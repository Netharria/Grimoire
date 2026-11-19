// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Helpers;
using Grimoire.Settings.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule(
    IDbContextFactory<SettingsDbContext> dbContextFactory,
    HybridCache cache,
    ILogger<SettingsModule> logger)
{
    /// <summary>How many times a document write re-reads and re-applies after losing a race (spec 2.2, section 2.3).</summary>
    private const int DocumentWriteAttempts = 3;

    private readonly HybridCache _cache = cache;

    private readonly HybridCacheEntryOptions _cacheEntryOptions = new() { Expiration = TimeSpan.FromDays(1) };

    private readonly IDbContextFactory<SettingsDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<SettingsModule> _logger = logger;

    /// <summary>
    ///     A guild's document, cached under its own key (C13). A missing document is <c>Empty</c>, which means every
    ///     setting in it is the default. One that doesn't parse fails rather than reading as defaults.
    /// </summary>
    private Task<Fin<T>> GetDocument<T>(GuildId guildId, CancellationToken cancellationToken)
        where T : class, ISettingsDocument<T>
        => ExecuteSafelyAsync(async ct => Fin.Succ<T>(
                await this._cache.GetOrCreateAsync(
                    CacheKey.Document(T.Kind, guildId),
                    (guildId, this._dbContextFactory),
                    static async (state, innerCt) =>
                    {
                        await using var dbContext = await state._dbContextFactory.CreateDbContextAsync(innerCt);
                        return ParseOrEmpty<T>(await LatestDocument(dbContext, state.guildId, T.Kind, innerCt));
                    },
                    this._cacheEntryOptions,
                    cancellationToken: ct)),
            $"Failed when retrieving the {T.Kind} settings for guild {guildId}",
            cancellationToken);

    /// <summary>
    ///     Applies <paramref name="change" /> to the latest version of a document and stores the result as the next
    ///     version (TD-6). A change that leaves the document as it was stores nothing. A write that loses a race with
    ///     another one re-reads and re-applies, so neither is lost. The cache key is cleared on every path (KI-103).
    /// </summary>
    private Task<Fin<Write<T>>> UpdateDocument<T>(
        GuildId guildId,
        ModeratorId setBy,
        Func<T, Fin<T>> change,
        CancellationToken cancellationToken)
        where T : class, ISettingsDocument<T>
        => ExecuteSafelyAsync<Write<T>>(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            for (var attempt = 0; attempt < DocumentWriteAttempts; attempt++)
            {
                var latest = await LatestDocument(dbContext, guildId, T.Kind, ct);
                var current = ParseOrEmpty<T>(latest);
                var changed = change(current);
                if (changed is Fin<T>.Fail(var error))
                    return error;
                var updated = (T)changed;

                // Value equality: on a generic T, == would compare references and see every write as a change.
                if (EqualityComparer<T>.Default.Equals(updated, current))
                {
                    await this._cache.RemoveAsync(CacheKey.Document(T.Kind, guildId), ct);
                    return new Write<T>.Unchanged("The setting was already set to that value.");
                }

                if (await InsertNextVersion(dbContext, guildId, T.Kind, latest?.Version ?? 0,
                        SettingsJson.Serialize(updated), setBy, ct))
                {
                    await this._cache.RemoveAsync(CacheKey.Document(T.Kind, guildId), ct);
                    return new Write<T>.Saved(updated);
                }
            }

            return new Unavailable($"The {T.Kind} settings kept changing while they were being saved.");
        }, "Was not able to save the setting to the database.", cancellationToken);

    private static T ParseOrEmpty<T>(GuildSettingRow? row) where T : class, ISettingsDocument<T>
        => row is null ? T.Empty : SettingsJson.Parse<T>(row.Value);

    private static Task<GuildSettingRow?> LatestDocument(SettingsDbContext dbContext, GuildId guildId,
        SettingsDocument document, CancellationToken cancellationToken)
        => dbContext.GuildSettings
            .AsNoTracking()
            .Where(row => row.GuildId == guildId && row.Document == document)
            .OrderByDescending(row => row.Version)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    ///     Inserts version <paramref name="expected" /> + 1, only if <paramref name="expected" /> is still the latest.
    ///     False means another write got there first. The race is in the statement, not in a caught exception: two
    ///     inserts that both pass the check meet the primary key, and <c>ON CONFLICT DO NOTHING</c> drops the second.
    /// </summary>
    private static async Task<bool> InsertNextVersion(SettingsDbContext dbContext, GuildId guildId,
        SettingsDocument document, int expected, string value, ModeratorId setBy, CancellationToken cancellationToken)
    {
        var guild = unchecked((long)guildId.Value);
        var name = document.ToString();
        var moderator = unchecked((long)setBy.Value);
        var setAt = DateTimeOffset.UtcNow;
        return await dbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO "Settings"."GuildSettings" ("GuildId", "Document", "Version", "Value", "SetBy", "SetAt")
            SELECT {guild}, {name}, {expected} + 1, CAST({value} AS jsonb), {moderator}, {setAt}
            WHERE COALESCE((SELECT max("Version") FROM "Settings"."GuildSettings"
                            WHERE "GuildId" = {guild} AND "Document" = {name}), 0) = {expected}
            ON CONFLICT DO NOTHING
            """, cancellationToken) == 1;
    }

    [LoggerMessage(LogLevel.Error, "Settings module operation failed: {message}")]
    private static partial void LogOperationFailure(ILogger logger, string message, Exception? ex);

    /// <summary>
    ///     The IO edge for settings (Q1): a thrown exception becomes <see cref="Unavailable" />, with the exception kept
    ///     for the log. <paramref name="failureMessage" /> is for the logs, not the user.
    /// </summary>
    private async Task<Fin<T>> ExecuteSafelyAsync<T>(
        Func<CancellationToken, Task<Fin<T>>> operation,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            return await operation(cancellationToken);
        }
        catch (Exception ex)
        {
            LogOperationFailure(this._logger, ex.Message, ex);
            return Unavailable.From(failureMessage, ex);
        }
    }

    public Task<Fin<Option<ChannelId>>> GetUserCommandChannel(GuildId guildId,
        CancellationToken cancellationToken = default)
        => GetDocument<GeneralConfig>(guildId, cancellationToken)
            .MapFin(general => Optional(general.UserCommandChannel));

    public Task<Fin<Write<Option<ChannelId>>>> SetUserCommandChannelSetting(
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken = default)
        => UpdateDocument<GeneralConfig>(guildId, moderatorId,
                general => general with { UserCommandChannel = channelId }, cancellationToken)
            .MapFin(write => write.Map(general => Optional(general.UserCommandChannel)));
}

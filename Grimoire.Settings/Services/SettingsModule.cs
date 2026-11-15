// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Globalization;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule(
    IDbContextFactory<SettingsDbContext> dbContextFactory,
    HybridCache cache,
    ILogger<SettingsModule> logger)
{
    private readonly HybridCache _cache = cache;

    private readonly HybridCacheEntryOptions _cacheEntryOptions = new() { Expiration = TimeSpan.FromDays(1) };

    private readonly IDbContextFactory<SettingsDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<SettingsModule> _logger = logger;

    private Task<Fin<CachedSetting>> GetGuildSetting(GuildSettingType key, GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct => Fin.Succ<CachedSetting>(
                await this._cache.GetOrCreateAsync(
                        CacheKey.GuildSetting(key, guildId),
                        new { key, guildId, this._dbContextFactory },
                        async (state, innerCt) =>
                        {
                            await using var dbContext = await state._dbContextFactory.CreateDbContextAsync(innerCt);
                            var result = await dbContext.GuildSettings
                                .AsNoTracking()
                                .Where(setting => setting.Type == state.key && setting.GuildId == state.guildId)
                                .OrderByDescending(setting => setting.SetAt)
                                .FirstOrDefaultAsync(innerCt);
                            return ToCachedSetting(result);
                        }, this._cacheEntryOptions,
                        cancellationToken: ct)),
            $"Failed when retrieving entry for {key} for guild {guildId}",
            cancellationToken);

    /// <remarks>
    ///     A failure is thrown, not swallowed: an empty result would read as "no custom settings", and the caller
    ///     caches what it builds from that.
    /// </remarks>
    private async Task<List<GuildSetting>> GetGuildSettings(
        GuildId guildId,
        FrozenSet<GuildSettingType> settingTypes,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.GuildSettings
            .AsNoTracking()
            .Where(s => s.GuildId == guildId && settingTypes.Contains(s.Type))
            .GroupBy(s => s.Type)
            .Select(g => g.OrderByDescending(s => s.SetAt).First())
            .ToListAsync(cancellationToken);
    }


    private static CachedSetting ToCachedSetting(GuildSetting? guildSetting) =>
        guildSetting switch
        {
            GuildSettingCustomValue customValue => new CachedCustomSetting(customValue.Value),
            GuildSettingDisabled => new CachedDisabledSetting(),
            _ => new CachedDefaultSetting()
        };

    private Task<Fin<Write<GuildSetting>>> SetGuildSetting(GuildSetting newSetting,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync<Write<GuildSetting>>(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var current = await dbContext.GuildSettings
                .AsNoTracking()
                .Where(s => s.GuildId == newSetting.GuildId && s.Type == newSetting.Type)
                .OrderByDescending(s => s.SetAt)
                .FirstOrDefaultAsync(ct);

            if (IsRedundantWrite(current, newSetting))
                return new Write<GuildSetting>.Unchanged("The setting was already set to that value.");
            dbContext.GuildSettings.Add(newSetting);
            await dbContext.SaveChangesAsync(ct);
            await this._cache.RemoveAsync(CacheKey.GuildSetting(newSetting.Type, newSetting.GuildId), ct);
            return new Write<GuildSetting>.Saved(newSetting);
        }, "Was not able to save the setting to the database.",
            cancellationToken);

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

    private static bool IsRedundantWrite(GuildSetting? current, GuildSetting incoming) =>
        (current, incoming) switch
        {
            (GuildSettingDisabled, GuildSettingDisabled) => true,
            (GuildSettingDefault, GuildSettingDefault) => true,
            (GuildSettingCustomValue a, GuildSettingCustomValue b) =>
                string.Equals(a.Value, b.Value, StringComparison.Ordinal),
            _ => false
        };

    public Task<Fin<Option<ChannelId>>> GetUserCommandChannel(GuildId guildId,
        CancellationToken cancellationToken = default)
        => GetGuildSetting(GuildSettingType.UserCommandChannel, guildId, cancellationToken)
            .MapFin(ParseChannelId);

    public Task<Fin<Write<Option<ChannelId>>>> SetUserCommandChannelSetting(
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken = default)
        => SetChannelSetting(GuildSettingType.UserCommandChannel, guildId, moderatorId, channelId, cancellationToken);

    /// <summary>Saves a channel setting. A null channel turns the setting off.</summary>
    private Task<Fin<Write<Option<ChannelId>>>> SetChannelSetting(
        GuildSettingType type,
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken)
        => ApplyGuildSetting(channelId is { } id
                    ? GuildSettingCustomValue.Create(type, guildId, moderatorId, DateTimeOffset.UtcNow,
                        id.Value.ToString(CultureInfo.InvariantCulture))
                    : Success<Error, GuildSetting>(
                        GuildSettingDisabled.Create(type, guildId, moderatorId, DateTimeOffset.UtcNow)),
                cancellationToken)
            .MapFin(write => write.Map(_ => Optional(channelId)));

    private Task<Fin<Write<GuildSetting>>> ApplyGuildSetting(Validation<Error, GuildSetting> validation,
        CancellationToken cancellationToken = default)
        => validation.ToFin().BindAsync(setting => SetGuildSetting(setting, cancellationToken));

    private static Option<T> ParseId<T>(CachedSetting setting, Func<ulong, T> create) =>
        setting is CachedCustomSetting { Value: var v }
        && ulong.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id != 0
            ? create(id)
            : None;

    private static Option<ChannelId> ParseChannelId(CachedSetting setting)
        => ParseId(setting, id => new ChannelId(id));

    private static Option<RoleId> ParseRoleId(CachedSetting setting)
        => ParseId(setting, id => new RoleId(id));

    private abstract record CachedSetting;

    private record CachedDefaultSetting : CachedSetting;

    private record CachedDisabledSetting : CachedSetting;

    private record CachedCustomSetting(string Value) : CachedSetting;
}

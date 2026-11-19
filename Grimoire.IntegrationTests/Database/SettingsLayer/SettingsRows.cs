// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Rows;

namespace Grimoire.IntegrationTests.Database.SettingsLayer;

/// <summary>
///     Rows to seed straight into the Settings tables (spec 2.2), for tests that set up a state the commands can't
///     reach, such as a lock that started hours ago. Mutes and locks are Active from their set time, as the commands
///     record them.
/// </summary>
internal static class SettingsRows
{
    public static ChannelLockRow ChannelLock(ModeratorId moderatorId, ModerationReason? reason, ChannelId channelId,
        GuildId guildId, DateTimeOffset setAt, PreviouslyAllowedPermissions previouslyAllowed,
        PreviouslyDeniedPermissions previouslyDenied, DateTimeOffset endTime)
        => new()
        {
            GuildId = guildId,
            ChannelId = channelId,
            Reason = reason,
            ModeratorId = moderatorId,
            SetAt = setAt,
            EndTime = endTime,
            PreviouslyAllowed = previouslyAllowed,
            PreviouslyDenied = previouslyDenied,
            State = nameof(ItemState.Active),
            ActivatedAt = setAt
        };

    public static ThreadLockRow ThreadLock(ModeratorId moderatorId, ModerationReason? reason, ChannelId channelId,
        GuildId guildId, DateTimeOffset setAt, DateTimeOffset endTime)
        => new()
        {
            GuildId = guildId,
            ChannelId = channelId,
            Reason = reason,
            ModeratorId = moderatorId,
            SetAt = setAt,
            EndTime = endTime,
            State = nameof(ItemState.Active),
            ActivatedAt = setAt
        };

    public static MuteRow Mute(UserId userId, GuildId guildId, ModeratorId moderatorId, SinId sinId,
        DateTimeOffset setAt, DateTimeOffset endTime, RoleId? muteRoleId = null)
        => new()
        {
            GuildId = guildId,
            UserId = userId,
            SinId = sinId,
            MuteRoleId = muteRoleId,
            ModeratorId = moderatorId,
            SetAt = setAt,
            EndTime = endTime,
            State = nameof(ItemState.Active),
            ActivatedAt = setAt
        };

    /// <summary>A mute a moderator ended at <paramref name="removedAt" />.</summary>
    public static MuteRow RemovedMute(UserId userId, GuildId guildId, ModeratorId moderatorId, SinId sinId,
        DateTimeOffset setAt, DateTimeOffset removedAt)
        => new()
        {
            GuildId = guildId,
            UserId = userId,
            SinId = sinId,
            ModeratorId = moderatorId,
            SetAt = setAt,
            EndTime = setAt.AddDays(1),
            State = nameof(ItemState.Removed),
            ActivatedAt = setAt,
            EndedAt = removedAt,
            RemovedBy = moderatorId
        };

    /// <summary>A version of a guild's settings document, as a write would store it.</summary>
    public static GuildSettingRow Document<T>(GuildId guildId, T document, int version = 1,
        ModeratorId? setBy = null, DateTimeOffset? setAt = null) where T : class, ISettingsDocument<T>
        => new()
        {
            GuildId = guildId,
            Document = T.Kind,
            Version = version,
            Value = SettingsJson.Serialize(document),
            SetBy = setBy ?? new ModeratorId(0),
            SetAt = setAt ?? DateTimeOffset.UtcNow
        };

    /// <summary>
    ///     Stores <paramref name="document" /> as the next version of the guild's document, as a direct database edit
    ///     would, without the settings module or its cache.
    /// </summary>
    public static async Task AddNextVersion<T>(SettingsDbContext dbContext, GuildId guildId, T document,
        CancellationToken cancellationToken) where T : class, ISettingsDocument<T>
    {
        var latest = await LatestDocument(dbContext, guildId, T.Kind, cancellationToken);
        dbContext.GuildSettings.Add(Document(guildId, document, (latest?.Version ?? 0) + 1));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The latest version of a guild's document, parsed, or Empty when it has none.</summary>
    public static async Task<T> LatestParsed<T>(SettingsDbContext dbContext, GuildId guildId,
        CancellationToken cancellationToken) where T : class, ISettingsDocument<T>
        => await LatestDocument(dbContext, guildId, T.Kind, cancellationToken) is { } row
            ? SettingsJson.Parse<T>(row.Value)
            : T.Empty;

    /// <summary>The latest version of a guild's document, or null when it has none.</summary>
    public static async Task<GuildSettingRow?> LatestDocument(SettingsDbContext dbContext, GuildId guildId,
        SettingsDocument document, CancellationToken cancellationToken)
        => await dbContext.GuildSettings.AsNoTracking()
            .Where(row => row.GuildId == guildId && row.Document == document)
            .OrderByDescending(row => row.Version)
            .FirstOrDefaultAsync(cancellationToken);
}

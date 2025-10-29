// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.


namespace Grimoire.Settings.Enums;

public enum GuildLogType
{
    Moderation,
    Leveling,
    MessageEdited,
    MessageDeleted,
    BulkMessageDeleted,
    UserJoined,
    UserLeft,
    AvatarUpdated,
    NicknameUpdated,
    UsernameUpdated,
    BanLog
}

public static class GuildLogTypeExtensions
{
    public static Module GetLogTypeModule(this GuildLogType guildLogType)
        => guildLogType switch
        {
            GuildLogType.Moderation => Module.General,
            GuildLogType.Leveling => Module.Leveling,
            GuildLogType.BulkMessageDeleted => Module.MessageLog,
            GuildLogType.MessageEdited => Module.MessageLog,
            GuildLogType.MessageDeleted => Module.MessageLog,
            GuildLogType.UserJoined => Module.UserLog,
            GuildLogType.UserLeft => Module.UserLog,
            GuildLogType.AvatarUpdated => Module.UserLog,
            GuildLogType.NicknameUpdated => Module.UserLog,
            GuildLogType.UsernameUpdated => Module.UserLog,
            GuildLogType.BanLog => Module.Moderation,
            _ => throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, null)
        };

    public static string GetCacheKey(this GuildLogType guildLogType, ulong guildId)
    {
        return guildLogType switch
        {
            GuildLogType.Moderation => $"ModerationLog-{guildId}",
            GuildLogType.Leveling => $"LevelingLog-{guildId}",
            GuildLogType.BulkMessageDeleted => $"BulkMessageDeletedLog-{guildId}",
            GuildLogType.MessageEdited => $"MessageEditedLog-{guildId}",
            GuildLogType.MessageDeleted => $"MessageDeletedLog-{guildId}",
            GuildLogType.UserJoined => $"UserJoinedLog-{guildId}",
            GuildLogType.UserLeft => $"UserLeftLog-{guildId}",
            GuildLogType.AvatarUpdated => $"AvatarUpdatedLog-{guildId}",
            GuildLogType.NicknameUpdated => $"NicknameUpdatedLog-{guildId}",
            GuildLogType.UsernameUpdated => $"UsernameUpdatedLog-{guildId}",
            GuildLogType.BanLog => $"BanLog-{guildId}",
            _ => throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, null)
        };
    }
}

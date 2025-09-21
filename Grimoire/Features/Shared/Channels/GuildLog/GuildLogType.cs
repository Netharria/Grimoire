// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.


namespace Grimoire.Features.Shared.Channels.GuildLog;

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
    UsernameUpdated
}

public static class GuildLogTypeExtensions
{
    public static Settings.Enums.Module GetLogTypeModule(this GuildLogType guildLogType)
        => guildLogType switch
        {
            GuildLogType.Moderation => Settings.Enums.Module.General,
            GuildLogType.Leveling => Settings.Enums.Module.Leveling,
            GuildLogType.BulkMessageDeleted => Settings.Enums.Module.MessageLog,
            GuildLogType.MessageEdited => Settings.Enums.Module.MessageLog,
            GuildLogType.MessageDeleted => Settings.Enums.Module.MessageLog,
            GuildLogType.UserJoined => Settings.Enums.Module.UserLog,
            GuildLogType.UserLeft => Settings.Enums.Module.UserLog,
            GuildLogType.AvatarUpdated => Settings.Enums.Module.UserLog,
            GuildLogType.NicknameUpdated => Settings.Enums.Module.UserLog,
            GuildLogType.UsernameUpdated => Settings.Enums.Module.UserLog,
            _ => throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, null)
        };
}

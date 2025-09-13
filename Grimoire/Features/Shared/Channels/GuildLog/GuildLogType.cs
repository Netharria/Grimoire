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
            _ => throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, null)
        };
}

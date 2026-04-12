// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.


using Grimoire.Settings.Domain;

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
    PublicModeration
}

public static class GuildLogTypeExtensions
{
    extension(GuildLogType guildLogType)
    {
        public Module GetLogTypeModule()
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
                GuildLogType.PublicModeration => Module.Moderation,
                _ => throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, null)
            };

        public GuildSettingType ToGuildSettingType()
            => guildLogType switch
            {
                GuildLogType.Moderation => GuildSettingType.ModerationLogChannel,
                GuildLogType.Leveling => GuildSettingType.LevelingLogChannel,
                GuildLogType.BulkMessageDeleted => GuildSettingType.BulkDeleteLogChannel,
                GuildLogType.MessageEdited => GuildSettingType.EditLogChannel,
                GuildLogType.MessageDeleted => GuildSettingType.DeleteLogChannel,
                GuildLogType.UserJoined => GuildSettingType.JoinLogChannel,
                GuildLogType.UserLeft => GuildSettingType.LeaveLogChannel,
                GuildLogType.AvatarUpdated => GuildSettingType.AvatarLogChannel,
                GuildLogType.NicknameUpdated => GuildSettingType.NicknameLogChannel,
                GuildLogType.UsernameUpdated => GuildSettingType.UsernameLogChannel,
                GuildLogType.PublicModeration => GuildSettingType.PublicModerationLogChannel,
                _ => throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, null)
            };
    }
}

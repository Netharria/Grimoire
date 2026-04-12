// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public enum GuildSettingType
{
    //Custom Command Settings
    CustomCommandsModuleEnabled,

    //General Settings
    ModerationLogChannel,
    UserCommandChannel,

    //Leveling Settings
    TextTime,
    LevelScalingBase,
    LevelScalingModifier,
    XpGainAmount,
    LevelingLogChannel,
    LevelingModuleEnabled,

    //Message Log Settings
    DeleteLogChannel,
    BulkDeleteLogChannel,
    EditLogChannel,
    MessageLogModuleEnabled,

    //Moderation Settings
    PublicModerationLogChannel,
    SinAutoPardonDuration,
    MuteRole,
    AntiSpamModuleEnabled,
    ModerationModuleEnabled,

    //User Log Settings
    JoinLogChannel,
    LeaveLogChannel,
    UsernameLogChannel,
    NicknameLogChannel,
    AvatarLogChannel,
    UserLogModuleEnabled
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Enums;

public enum Module
{
    Leveling,
    UserLog,
    Moderation,
    MessageLog,
    Commands,
    General,
    AntiSpam
}

internal static class ModuleExtensions
{
    public static GuildSettingType? ToGuildSettingType(this Module module)
        => module switch
        {
            Module.Commands => GuildSettingType.CustomCommandsModuleEnabled,
            Module.Leveling => GuildSettingType.LevelingModuleEnabled,
            Module.MessageLog => GuildSettingType.MessageLogModuleEnabled,
            Module.Moderation => GuildSettingType.ModerationModuleEnabled,
            Module.UserLog => GuildSettingType.UserLogModuleEnabled,
            Module.AntiSpam => GuildSettingType.AntiSpamModuleEnabled,
            Module.General => null,
            _ => throw new ArgumentOutOfRangeException(nameof(module), module, null)
        };
}

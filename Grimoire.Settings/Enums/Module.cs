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
    General
}

internal static class ModuleExtensions
{
    public static string GetCacheKey(this Module ignoredType, ulong guildId)
    {
        return ignoredType switch
        {
            Module.Leveling => $"LevelingModule-{guildId}",
            Module.UserLog => $"UserLogModule-{guildId}",
            Module.Moderation => $"ModerationModule-{guildId}",
            Module.MessageLog => $"MessageLogModule-{guildId}",
            Module.Commands => $"CommandsModule-{guildId}",
            Module.General => $"GeneralModule-{guildId}",
            _ => throw new ArgumentOutOfRangeException(nameof(ignoredType), ignoredType, null)
        };
    }
}

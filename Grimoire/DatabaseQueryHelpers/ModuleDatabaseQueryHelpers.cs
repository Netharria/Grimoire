// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.DatabaseQueryHelpers;

public static class ModuleDatabaseQueryHelpers
{
    public static IQueryable<IModule> GetModulesOfType(this IQueryable<Guild> databaseGuilds, Module module) =>
        module switch
        {
            Module.Leveling => databaseGuilds.Select(x => x.LevelSettings),
            Module.UserLog => databaseGuilds.Select(x => x.UserLogSettings),
            Module.Moderation => databaseGuilds.Select(x => x.ModerationSettings),
            Module.MessageLog => databaseGuilds.Select(x => x.MessageLogSettings),
            Module.Commands => databaseGuilds.Select(x => x.CommandsSettings),
            _ => throw new ArgumentOutOfRangeException(nameof(module), module, null)
        };
}

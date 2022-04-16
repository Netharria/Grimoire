// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cybermancy.Core.Enums;
using Cybermancy.Domain;
using Cybermancy.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.DatabaseQueryHelpers
{
    public static class IModuleDatabaseQueryHelpers
    {
        public static IQueryable<IModule> GetModulesOfType(this IQueryable<Guild> databaseGuilds, Module module, CancellationToken cancellationToken = default)
        {
            return module switch
            {
                Module.Leveling => databaseGuilds.Select(x => x.LevelSettings),
                Module.Logging => databaseGuilds.Select(x => x.LogSettings),
                Module.Moderation => databaseGuilds.Select(x => x.ModerationSettings),
                _ => throw new ArgumentOutOfRangeException(nameof(module), module, message: null)
            };
        }
    }
}

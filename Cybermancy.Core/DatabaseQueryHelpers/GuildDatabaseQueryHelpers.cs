// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.SharedDtos;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.DatabaseQueryHelpers
{
    public static class GuildDatabaseQueryHelpers
    {
        public static async Task<bool> AddMissingGuildsAsync(this DbSet<Guild> databaseGuilds, IEnumerable<GuildDto> guilds, CancellationToken cancellationToken = default)
        {
            var guildsToAdd = guilds
                .ExceptBy(
                databaseGuilds.Select(x => x.Id),
                x => x.Id)
                .Select(x => new Guild
                {
                    Id = x.Id,
                    LevelSettings = new GuildLevelSettings(),
                    ModerationSettings = new GuildModerationSettings(),
                    LogSettings = new GuildLogSettings(),
                });

            if (guildsToAdd.Any())
                await databaseGuilds.AddRangeAsync(guildsToAdd, cancellationToken);

            return guildsToAdd.Any();
        }
    }
}

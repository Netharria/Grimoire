// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.DatabaseQueryHelpers;

public static class GuildDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingGuildsAsync(this DbSet<Guild> databaseGuilds, IEnumerable<GuildDto> guilds, CancellationToken cancellationToken = default)
    {
        var guildsToAdd = guilds
            .ExceptBy(
            databaseGuilds
            .AsNoTracking().Select(x => x.Id),
            x => x.Id)
            .Select(x => new Guild
            {
                Id = x.Id,
                LevelSettings = new GuildLevelSettings(),
                ModerationSettings = new GuildModerationSettings(),
                UserLogSettings = new GuildUserLogSettings(),
                MessageLogSettings = new GuildMessageLogSettings(),
            });

        if (guildsToAdd.Any())
            await databaseGuilds.AddRangeAsync(guildsToAdd, cancellationToken);

        return guildsToAdd.Any();
    }
}

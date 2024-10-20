// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class GuildDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingGuildsAsync(this DbSet<Guild> databaseGuilds, IEnumerable<GuildDto> guilds, CancellationToken cancellationToken = default)
    {

        var incomingGuilds = guilds
            .Select(x => x.Id);

        var existingGuildIds = await databaseGuilds
            .AsNoTracking()
            .Where(x => incomingGuilds.Contains(x.Id))
            .Select(x => x.Id)
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);

        var guildsToAdd = guilds
            .Where(x => !existingGuildIds.Contains(x.Id))
            .Select(x => new Guild
            {
                Id = x.Id,
                LevelSettings = new GuildLevelSettings(),
                ModerationSettings = new GuildModerationSettings(),
                UserLogSettings = new GuildUserLogSettings(),
                MessageLogSettings = new GuildMessageLogSettings(),
                CommandsSettings = new GuildCommandsSettings(),
            });

        if (guildsToAdd.Any())
        {
            await databaseGuilds.AddRangeAsync(guildsToAdd, cancellationToken);
            return true;
        }
        return false;
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class GuildDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingGuildsAsync(this DbSet<Guild> databaseGuilds,
        IReadOnlyCollection<ulong> guilds, CancellationToken cancellationToken = default)
    {
        var existingGuildIds = await databaseGuilds
            .AsNoTracking()
            .Where(x => guilds.Contains(x.Id))
            .Select(x => x.Id)
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);

        var guildsToAdd = guilds
            .Where(x => !existingGuildIds.Contains(x))
            .Select(x => new Guild
            {
                Id = x,
            }).ToArray().AsReadOnly();

        if (guildsToAdd.Count == 0) return false;

        await databaseGuilds.AddRangeAsync(guildsToAdd, cancellationToken);
        return true;
    }
}

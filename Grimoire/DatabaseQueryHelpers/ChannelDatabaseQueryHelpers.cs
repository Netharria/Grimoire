// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class ChannelDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingChannelsAsync(this DbSet<Channel> databaseChannels,
        IReadOnlyCollection<ChannelDto> channels, CancellationToken cancellationToken = default)
    {
        var incomingChannelIds = channels.Select(x => x.Id);

        var existingChannelIds = await databaseChannels
            .AsNoTracking()
            .Where(x => incomingChannelIds.Contains(x.Id))
            .Select(x => x.Id)
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);

        var channelsToAdd = channels
            .Where(x => !existingChannelIds.Contains(x.Id))
            .Select(x => new Channel { Id = x.Id, GuildId = x.GuildId }).ToArray().AsReadOnly();

        if (channelsToAdd.Count == 0)
            return false;
        await databaseChannels.AddRangeAsync(channelsToAdd, cancellationToken);
        return true;
    }
}

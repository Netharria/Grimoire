// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.DatabaseQueryHelpers;

public static class ChannelDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingChannelsAsync(this DbSet<Channel> databaseChannels, IEnumerable<ChannelDto> channels, CancellationToken cancellationToken = default)
    {
        var incomingChannels = channels
            .Select(x => new Channel
            {
                Id = x.Id,
                GuildId = x.GuildId
            });

        var channelsToAdd = incomingChannels.ExceptBy(databaseChannels
            .AsNoTracking()
            .Where(x => incomingChannels.Contains(x))
            .Select(x => x.Id), x => x.Id);

        if (channelsToAdd.Any())
            await databaseChannels.AddRangeAsync(channelsToAdd, cancellationToken);
        return channelsToAdd.Any();
    }
}

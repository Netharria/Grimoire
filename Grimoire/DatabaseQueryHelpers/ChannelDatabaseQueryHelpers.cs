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
        DiscordGuild discordGuild, CancellationToken cancellationToken = default)
    {

        var existingChannelIds = await databaseChannels
            .AsNoTracking()
            .Where(x => discordGuild.Channels.Keys.Contains(x.Id))
            .Select(x => x.Id)
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);

        var channelsToAdd = discordGuild.Channels.Keys
            .Where(x => !existingChannelIds.Contains(x))
            .Select(x => new Channel { Id = x, GuildId = discordGuild.Id })
            .ToArray();

        if (channelsToAdd.Length == 0)
            return false;
        await databaseChannels.AddRangeAsync(channelsToAdd, cancellationToken);
        return true;
    }
}

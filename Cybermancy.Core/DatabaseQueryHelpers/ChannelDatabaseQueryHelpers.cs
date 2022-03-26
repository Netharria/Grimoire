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
    public static class ChannelDatabaseQueryHelpers
    {
        public static async Task<bool> AddMissingChannelsAsync(this DbSet<Channel> databaseChannels, IEnumerable<ChannelDto> channels, CancellationToken cancellationToken)
        {
            var channelsToAdd = channels
                .ExceptBy(databaseChannels.Select(x => x.Id),
                x => x.Id)
                .Select(x => new Channel
                {
                    Id = x.Id,
                    GuildId = x.GuildId
                });

            if (channelsToAdd.Any())
                await databaseChannels.AddRangeAsync(channelsToAdd, cancellationToken);
            return channelsToAdd.Any();
        }
    }
}

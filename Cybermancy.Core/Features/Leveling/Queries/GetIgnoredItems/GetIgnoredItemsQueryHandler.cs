// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Core.Contracts.Persistance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Queries.GetIgnoredItems
{
    public class GetIgnoredItemsQueryHandler : IRequestHandler<GetIgnoredItemsQuery, GetIgnoredItemsQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetIgnoredItemsQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<GetIgnoredItemsQueryResponse> Handle(GetIgnoredItemsQuery request, CancellationToken cancellationToken)
        {

            var ignoredItems = await this._cybermancyDbContext.Guilds
                .Where(x => x.Id == request.GuildId)
                .Select(x => new
                {
                    IgnoredRoles = x.Roles.Where(x => x.IsXpIgnored).Select(x => x.Id),
                    IgnoredChannels = x.Channels.Where(x => x.IsXpIgnored).Select(x => x.Id),
                    IgnoredGuildUsers = x.GuildUsers.Where(x => x.IsXpIgnored).Select(x => x.UserId)
                }).FirstAsync(cancellationToken: cancellationToken);

            if (!ignoredItems.IgnoredRoles.Any() && !ignoredItems.IgnoredChannels.Any() && !ignoredItems.IgnoredGuildUsers.Any())
                return new GetIgnoredItemsQueryResponse { Success = false, Message = "This server does not have any ignored channels, roles or users." };

            var ignoredMessageBuilder = new StringBuilder().Append("**Channels**\n");
            foreach (var channel in ignoredItems.IgnoredChannels) ignoredMessageBuilder.Append($"<#{channel}>").Append('\n');

            ignoredMessageBuilder.Append("\n**Roles**\n");
            foreach (var role in ignoredItems.IgnoredRoles) ignoredMessageBuilder.Append($"<@&{role}>").Append('\n');

            ignoredMessageBuilder.Append("\n**Users**\n");
            foreach (var user in ignoredItems.IgnoredGuildUsers) ignoredMessageBuilder.Append($"<@!{user}>").Append('\n');

            return new GetIgnoredItemsQueryResponse { Success = true, ResultString = ignoredMessageBuilder.ToString() };
        }
    }
}

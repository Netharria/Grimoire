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
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Extensions;

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
                .WhereIdIs(request.GuildId)
                .Select(x => new
                {
                    IgnoredRoles = x.Roles.WhereIgnored(true).Select(x => x.Id),
                    IgnoredChannels = x.Channels.Where(x => x.IsXpIgnored).Select(x => x.Id),
                    IgnoredMembers = x.Members.Where(x => x.IsXpIgnored).Select(x => x.UserId)
                }).FirstAsync(cancellationToken: cancellationToken);

            if (!ignoredItems.IgnoredRoles.Any() && !ignoredItems.IgnoredChannels.Any() && !ignoredItems.IgnoredMembers.Any())
                return new GetIgnoredItemsQueryResponse { Success = false, Message = "This server does not have any ignored channels, roles or users." };

            var ignoredMessageBuilder = new StringBuilder().Append("**Channels**\n");
            foreach (var channel in ignoredItems.IgnoredChannels) ignoredMessageBuilder.Append(ChannelExtensions.Mention(channel)).Append('\n');

            ignoredMessageBuilder.Append("\n**Roles**\n");
            foreach (var role in ignoredItems.IgnoredRoles) ignoredMessageBuilder.Append(RoleExtensions.Mention(role)).Append('\n');

            ignoredMessageBuilder.Append("\n**Users**\n");
            foreach (var member in ignoredItems.IgnoredMembers) ignoredMessageBuilder.Append(UserExtensions.Mention(member)).Append('\n');

            return new GetIgnoredItemsQueryResponse { Success = true, ResultString = ignoredMessageBuilder.ToString() };
        }
    }
}

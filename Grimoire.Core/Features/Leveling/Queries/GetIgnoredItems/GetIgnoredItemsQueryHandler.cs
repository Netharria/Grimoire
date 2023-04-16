// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Queries.GetIgnoredItems
{
    public class GetIgnoredItemsQueryHandler : IRequestHandler<GetIgnoredItemsQuery, BaseResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public GetIgnoredItemsQueryHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<BaseResponse> Handle(GetIgnoredItemsQuery request, CancellationToken cancellationToken)
        {

            var ignoredItems = await this._grimoireDbContext.Guilds
                .WhereIdIs(request.GuildId)
                .Select(x => new
                {
                    IgnoredRoles = x.Roles.Where(x => x.IsXpIgnored).Select(x => x.Id),
                    IgnoredChannels = x.Channels.Where(x => x.IsXpIgnored).Select(x => x.Id),
                    IgnoredMembers = x.Members.Where(x => x.IsXpIgnored).Select(x => x.UserId)
                }).FirstAsync(cancellationToken: cancellationToken);

            if (!ignoredItems.IgnoredRoles.Any() && !ignoredItems.IgnoredChannels.Any() && !ignoredItems.IgnoredMembers.Any())
                throw new AnticipatedException ("This server does not have any ignored channels, roles or users." );

            var ignoredMessageBuilder = new StringBuilder().Append("**Channels**\n");
            foreach (var channel in ignoredItems.IgnoredChannels) ignoredMessageBuilder.Append(ChannelExtensions.Mention(channel)).Append('\n');

            ignoredMessageBuilder.Append("\n**Roles**\n");
            foreach (var role in ignoredItems.IgnoredRoles) ignoredMessageBuilder.Append(RoleExtensions.Mention(role)).Append('\n');

            ignoredMessageBuilder.Append("\n**Users**\n");
            foreach (var member in ignoredItems.IgnoredMembers) ignoredMessageBuilder.Append(UserExtensions.Mention(member)).Append('\n');

            return new BaseResponse { Message = ignoredMessageBuilder.ToString() };
        }
    }
}

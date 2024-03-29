// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Queries;

public sealed class GetIgnoredItems
{
    public sealed record Query : IRequest<BaseResponse>
    {
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Query request, CancellationToken cancellationToken)
        {

            var ignoredItems = await this._grimoireDbContext.Guilds
            .AsNoTracking()
            .AsSplitQuery()
            .WhereIdIs(request.GuildId)
            .Select(x => new
            {
                IgnoredRoles = x.IgnoredRoles.Select(x => x.RoleId),
                IgnoredChannels = x.IgnoredChannels.Select(x => x.ChannelId),
                IgnoredMembers = x.IgnoredMembers.Select(x => x.UserId)
            }).FirstAsync(cancellationToken: cancellationToken);

            if (!ignoredItems.IgnoredRoles.Any() && !ignoredItems.IgnoredChannels.Any() && !ignoredItems.IgnoredMembers.Any())
                throw new AnticipatedException("This server does not have any ignored channels, roles or users.");

            var ignoredMessageBuilder = new StringBuilder().Append("**Channels**\n");

            foreach (var channel in ignoredItems.IgnoredChannels)
                ignoredMessageBuilder.Append(ChannelExtensions.Mention(channel)).Append('\n');

            ignoredMessageBuilder.Append("\n**Roles**\n");

            foreach (var role in ignoredItems.IgnoredRoles)
                ignoredMessageBuilder.Append(RoleExtensions.Mention(role)).Append('\n');

            ignoredMessageBuilder.Append("\n**Users**\n");

            foreach (var member in ignoredItems.IgnoredMembers)
                ignoredMessageBuilder.Append(UserExtensions.Mention(member)).Append('\n');

            return new BaseResponse { Message = ignoredMessageBuilder.ToString() };
        }
    }

}


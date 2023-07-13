// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Shared.Queries.GetModuleAndCommandOverrideStateForGuild;
public class GetCommandEnabledQueryHandler : IQueryHandler<GetCommandEnabledQuery, bool>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public GetCommandEnabledQueryHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<bool> Handle(GetCommandEnabledQuery query, CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.Guilds
                .WhereIdIs(query.GuildId)
                .Select(x => new
                {
                    RoleEnabled = x.RoleCommandOverrides.Where(x => query.RoleIds.Contains(x.RoleId))
                    .Where(x => x.ChannelId == null || x.ChannelId == query.ChannelId)
                    .Any(x => x.CommandPermissions.HasFlag(query.Permissions)),
                    MemberEnabled = x.MemberCommandOverrides
                    .Where(x => x.UserId == query.UserId && x.GuildId == query.GuildId)
                    .Where(x => x.ChannelId == null || x.ChannelId == query.ChannelId)
                    .Any(x => x.CommandPermissions.HasFlag(query.Permissions)),
                }).FirstAsync(cancellationToken: cancellationToken);
        return result.MemberEnabled || result.RoleEnabled;
    }
}

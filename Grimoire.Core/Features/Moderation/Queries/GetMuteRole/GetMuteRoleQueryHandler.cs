// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries.GetMuteRole;

public class GetMuteRoleQueryHandler : IRequestHandler<GetMuteRoleQuery, GetMuteRoleQueryResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public GetMuteRoleQueryHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<GetMuteRoleQueryResponse> Handle(GetMuteRoleQuery request, CancellationToken cancellationToken)
    {
        var muteRoleId = await this._grimoireDbContext.GuildModerationSettings
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => x.MuteRole)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        if (muteRoleId is null) throw new AnticipatedException("No mute role is configured.");
        return new GetMuteRoleQueryResponse { RoleId = muteRoleId.Value };
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries;

public sealed record GetMuteRoleQuery : IRequest<GetMuteRoleQueryResponse>
{
    public ulong GuildId { get; init; }
}

public sealed class GetMuteRoleQueryHandler(IGrimoireDbContext grimoireDbContext) : IRequestHandler<GetMuteRoleQuery, GetMuteRoleQueryResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<GetMuteRoleQueryResponse> Handle(GetMuteRoleQuery request, CancellationToken cancellationToken)
    {
        var muteRoleId = await this._grimoireDbContext.GuildModerationSettings
            .AsNoTracking()
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => x.MuteRole)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        if (muteRoleId is null) throw new AnticipatedException("No mute role is configured.");
        return new GetMuteRoleQueryResponse { RoleId = muteRoleId.Value };
    }
}

public sealed record GetMuteRoleQueryResponse : BaseResponse
{
    public ulong RoleId { get; init; }
}

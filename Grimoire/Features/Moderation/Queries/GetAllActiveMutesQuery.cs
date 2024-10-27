// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Queries;

public sealed record GetAllActiveMutesQuery : IRequest<GetAllActiveMutesQueryResponse>
{
    public ulong GuildId { get; init; }
}

public sealed class GetAllActiveMutesQueryHandler(GrimoireDbContext grimoireDbContext)
    : IRequestHandler<GetAllActiveMutesQuery, GetAllActiveMutesQueryResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task<GetAllActiveMutesQueryResponse> Handle(GetAllActiveMutesQuery request,
        CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.GuildModerationSettings
            .AsNoTracking()
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => new GetAllActiveMutesQueryResponse
            {
                MuteRole = x.MuteRole, MutedUsers = x.Guild.ActiveMutes.Select(x => x.UserId).ToArray()
            }).FirstOrDefaultAsync(cancellationToken);
        if (result is null)
            throw new AnticipatedException("Could not find the settings for this server.");
        return result;
    }
}

public sealed record GetAllActiveMutesQueryResponse : BaseResponse
{
    public ulong? MuteRole { get; init; }
    public ulong[] MutedUsers { get; init; } = [];
}

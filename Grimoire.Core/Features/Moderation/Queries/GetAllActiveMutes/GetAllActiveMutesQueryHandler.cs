// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries.GetAllActiveMutes;

public class GetAllActiveMutesQueryHandler(IGrimoireDbContext grimoireDbContext) : IQueryHandler<GetAllActiveMutesQuery, GetAllActiveMutesQueryResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<GetAllActiveMutesQueryResponse> Handle(GetAllActiveMutesQuery request, CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.GuildModerationSettings
            .AsNoTracking()
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => new GetAllActiveMutesQueryResponse
            {
                MuteRole = x.MuteRole,
                MutedUsers = x.Guild.ActiveMutes.Select(x => x.UserId).ToArray(),
            }).FirstOrDefaultAsync(cancellationToken);
        if (result is null)
            throw new AnticipatedException("Could not find the settings for this server.");
        return result;
    }
}

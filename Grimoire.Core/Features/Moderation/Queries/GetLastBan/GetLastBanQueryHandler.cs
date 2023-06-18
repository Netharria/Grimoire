// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Moderation.Queries.GetLastBan;

public class GetLastBanQueryHandler : IRequestHandler<GetLastBanQuery, GetLastBanQueryResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public GetLastBanQueryHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<GetLastBanQueryResponse> Handle(GetLastBanQuery request, CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.Sins
            .WhereMemberHasId(request.UserId, request.GuildId)
            .Where(x => x.SinType == SinType.Ban)
            .OrderByDescending(x => x.SinOn)
            .Select(x => new GetLastBanQueryResponse
            {
                UserId = x.UserId,
                GuildId = x.GuildId,
                ModeratorId = x.ModeratorId,
                Reason = x.Reason,
                SinId = x.Id,
                SinOn = x.SinOn,
                LogChannelId = x.Guild.ModChannelLog,
                ModerationModuleEnabled = x.Guild.ModerationSettings.ModuleEnabled
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (result is null)
            return new GetLastBanQueryResponse();

        return result;
    }
}

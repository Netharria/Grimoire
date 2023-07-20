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
        var result = await this._grimoireDbContext.Members
            .AsNoTracking()
            .WhereMemberHasId(request.UserId, request.GuildId)
            .Select(member => new GetLastBanQueryResponse
            {
                UserId = member.UserId,
                GuildId = member.GuildId,
                LastSin = member.UserSins.OrderByDescending(x => x.SinOn)
                        .Where(sin => sin.SinType == SinType.Ban)
                        .Select(sin => new LastSin
                        {
                            SinId = sin.Id,
                            ModeratorId = sin.ModeratorId,
                            Reason = sin.Reason,
                            SinOn = sin.SinOn
                        })
                    .FirstOrDefault(),

                LogChannelId = member.Guild.ModChannelLog,
                ModerationModuleEnabled = member.Guild.ModerationSettings.ModuleEnabled
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (result is null)
            return new GetLastBanQueryResponse();

        return result;
    }
}

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Moderation.Queries.GetBan
{
    public class GetLastBanQueryHandler : IRequestHandler<GetLastBanQuery, GetLastBanQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetLastBanQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<GetLastBanQueryResponse> Handle(GetLastBanQuery request, CancellationToken cancellationToken)
        {
            var result = await this._cybermancyDbContext.Sins
                .WhereMemberHasId(request.UserId, request.GuildId)
                .Where(x => x.SinType == SinType.Ban)
                .OrderByDescending(x => x.InfractionOn)
                .Select(x => new GetLastBanQueryResponse
                {
                    UserId = x.UserId,
                    GuildId = x.GuildId,
                    ModeratorId = x.ModeratorId,
                    Reason = x.Reason,
                    SinId = x.Id,
                    SinOn = x.InfractionOn,
                    LogChannelId = x.Guild.ModChannelLog,
                    ModerationModuleEnabled = x.Guild.ModerationSettings.ModuleEnabled
                })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (result is null)
                return new GetLastBanQueryResponse();

            return result;
        }
    }
}

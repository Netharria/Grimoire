// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Queries.GetLevel
{
    public class GetLevelQueryHandler : IRequestHandler<GetLevelQuery, GetLevelQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetLevelQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<GetLevelQueryResponse> Handle(GetLevelQuery request, CancellationToken cancellationToken)
        {
            var member = await this._cybermancyDbContext.Members
                .WhereMemberHasId(request.UserId, request.GuildId)
                .Include(x => x.Guild.LevelSettings)
                .Select(x => new { Xp = x.XpHistory.Sum(x => x.Xp),
                    Level = x.GetLevel(),
                    LevelProgress = x.XpHistory.Sum(x => x.Xp) - x.GetXpNeeded(),
                    TotalXpRequiredToLevel = x.GetXpNeeded(1) - x.GetXpNeeded()
                }).FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (member is null)
                return new GetLevelQueryResponse()
                {
                    Success = false,
                    Message = "That user could not be found."
                };

            var nextReward = await this._cybermancyDbContext.Rewards
                .Where(x => x.GuildId == request.GuildId && x.RewardLevel > member.Level)
                .OrderBy(x => x.RewardLevel)
                .Select(x => new { x.RoleId, x.RewardLevel })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            return new GetLevelQueryResponse
            {
                Success = true,
                UsersXp = member.Xp,
                UsersLevel = member.Level,
                LevelProgress = member.LevelProgress,
                XpForNextLevel = member.TotalXpRequiredToLevel,
                NextRoleRewardId = nextReward?.RoleId,
                NextRewardLevel = nextReward?.RewardLevel
            };
        }
    }
}

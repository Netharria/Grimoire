// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Queries.GetLevel
{
    public class GetLevelQueryHandler : IRequestHandler<GetLevelQuery, GetLevelQueryResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public GetLevelQueryHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<GetLevelQueryResponse> Handle(GetLevelQuery request, CancellationToken cancellationToken)
        {
            var member = await this._grimoireDbContext.Members
                .WhereMemberHasId(request.UserId, request.GuildId)
                .Include(x => x.Guild.LevelSettings)
                .Select(x => new { Xp = x.XpHistory.Sum(x => x.Xp),
                    Level = x.GetLevel(),
                    LevelProgress = x.XpHistory.Sum(x => x.Xp) - x.GetXpNeeded(),
                    TotalXpRequiredToLevel = x.GetXpNeeded(1) - x.GetXpNeeded()
                }).FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (member is null)
                throw new AnticipatedException("That user could not be found.");

            var nextReward = await this._grimoireDbContext.Rewards
                .Where(x => x.GuildId == request.GuildId && x.RewardLevel > member.Level)
                .OrderBy(x => x.RewardLevel)
                .Select(x => new { x.RoleId, x.RewardLevel })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            return new GetLevelQueryResponse
            {
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

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
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
            var guildUser = await this._cybermancyDbContext.GuildUsers
                .Where(x => x.UserId == request.UserId && x.GuildId == request.GuildId)
                .Select(x => new { x.Xp,
                    Level = x.GetLevel(x.Guild.LevelSettings.Base, x.Guild.LevelSettings.Modifier),
                    LevelProgress = x.Xp - x.GetXpNeeded(x.Guild.LevelSettings.Base, x.Guild.LevelSettings.Modifier, 0),
                    TotalXpRequiredToLevel = x.GetXpNeeded(x.Guild.LevelSettings.Base, x.Guild.LevelSettings.Modifier, 1) -
                        x.GetXpNeeded(x.Guild.LevelSettings.Base, x.Guild.LevelSettings.Modifier, 0)
                }).FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (guildUser is null)
                return new GetLevelQueryResponse()
                {
                    Success = false,
                    Message = "That user could not be found."
                };

            var nextReward = await this._cybermancyDbContext.Rewards
                .Where(x => x.GuildId == request.GuildId && x.RewardLevel > guildUser.Level)
                .OrderBy(x => x.RewardLevel)
                .Select(x => new { x.RoleId, x.RewardLevel })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            return new GetLevelQueryResponse
            {
                Success = true,
                UsersXp = guildUser.Xp,
                UsersLevel = guildUser.Level,
                LevelProgress = guildUser.LevelProgress,
                TotalXpRequiredToLevel = guildUser.TotalXpRequiredToLevel,
                NextRoleRewardId = nextReward?.RoleId,
                NextRewardLevel = nextReward?.RewardLevel
            };
        }
    }
}

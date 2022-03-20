// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Extensions;
using Cybermancy.Core.Responses;
using Cybermancy.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Commands.MangeRewardsCommands.AddReward
{
    public class AddRewardCommandHandler : IRequestHandler<AddRewardCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AddRewardCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<BaseResponse> Handle(AddRewardCommand request, CancellationToken cancellationToken)
        {
            var reward = await this._cybermancyDbContext.Rewards.FirstOrDefaultAsync(x => x.RoleId == request.RoleId, cancellationToken: cancellationToken);
            if (reward is null)
            {
                reward = new Reward
                {
                    GuildId = request.GuildId,
                    RoleId = request.RoleId,
                    RewardLevel = request.RewardLevel
                };
                await this._cybermancyDbContext.Rewards.AddAsync(reward, cancellationToken);
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
                return new BaseResponse { Success = true, Message = $"Added {reward.Mention()} reward at level {request.RewardLevel}" };
            }

            reward.RewardLevel = request.RewardLevel;
            this._cybermancyDbContext.Rewards.Update(reward);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { Success = true, Message = $"Updated {reward.Mention()} reward to level {request.RewardLevel}" };
        }
    }
}

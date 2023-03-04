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
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Commands.ManageRewardsCommands.AddReward
{
    public class AddRewardCommandHandler : ICommandHandler<AddRewardCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AddRewardCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<BaseResponse> Handle(AddRewardCommand command, CancellationToken cancellationToken)
        {
            var reward = await this._cybermancyDbContext.Rewards.FirstOrDefaultAsync(x => x.RoleId == command.RoleId, cancellationToken: cancellationToken);
            if (reward is null)
            {
                reward = new Reward
                {
                    GuildId = command.GuildId,
                    RoleId = command.RoleId,
                    RewardLevel = command.RewardLevel
                };
                await this._cybermancyDbContext.Rewards.AddAsync(reward, cancellationToken);
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
                return new BaseResponse { Message = $"Added {reward.Mention()} reward at level {command.RewardLevel}" };
            }

            reward.RewardLevel = command.RewardLevel;
            this._cybermancyDbContext.Rewards.Update(reward);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { Message = $"Updated {reward.Mention()} reward to level {command.RewardLevel}" };
        }
    }
}

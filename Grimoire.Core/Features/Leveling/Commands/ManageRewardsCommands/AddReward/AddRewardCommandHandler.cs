// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Commands.ManageRewardsCommands.AddReward
{
    public class AddRewardCommandHandler : ICommandHandler<AddRewardCommand, BaseResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public AddRewardCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<BaseResponse> Handle(AddRewardCommand command, CancellationToken cancellationToken)
        {
            var reward = await this._grimoireDbContext.Rewards.FirstOrDefaultAsync(x => x.RoleId == command.RoleId, cancellationToken: cancellationToken);
            if (reward is null)
            {
                reward = new Reward
                {
                    GuildId = command.GuildId,
                    RoleId = command.RoleId,
                    RewardLevel = command.RewardLevel
                };
                await this._grimoireDbContext.Rewards.AddAsync(reward, cancellationToken);
                await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
                return new BaseResponse { Message = $"Added {reward.Mention()} reward at level {command.RewardLevel}" };
            }

            reward.RewardLevel = command.RewardLevel;
            this._grimoireDbContext.Rewards.Update(reward);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { Message = $"Updated {reward.Mention()} reward to level {command.RewardLevel}" };
        }
    }
}

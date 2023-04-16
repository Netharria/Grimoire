// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Extensions;

namespace Cybermancy.Core.Features.Leveling.Commands.ManageRewardsCommands.RemoveReward
{
    public class RemoveRewardCommandHandler : ICommandHandler<RemoveRewardCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public RemoveRewardCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<BaseResponse> Handle(RemoveRewardCommand command, CancellationToken cancellationToken)
        {
            var result = await this._cybermancyDbContext.Rewards
                .FirstOrDefaultAsync(x => x.RoleId == command.RoleId, cancellationToken);
            if (result is not Reward reward)
                throw new AnticipatedException($"Did not find a saved reward for role <@&{command.RoleId}>");
            this._cybermancyDbContext.Rewards.Remove(reward);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { Message = $"Removed {reward.Mention()} reward" };
        }
    }
}

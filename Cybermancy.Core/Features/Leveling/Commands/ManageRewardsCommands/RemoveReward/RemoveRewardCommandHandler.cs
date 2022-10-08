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

namespace Cybermancy.Core.Features.Leveling.Commands.ManageRewardsCommands.RemoveReward
{
    public class RemoveRewardCommandHandler : IRequestHandler<RemoveRewardCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public RemoveRewardCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<BaseResponse> Handle(RemoveRewardCommand request, CancellationToken cancellationToken)
        {
            var result = await this._cybermancyDbContext.Rewards
                .FirstOrDefaultAsync(x => x.RoleId == request.RoleId, cancellationToken);
            if (result is not Reward reward)
                return new BaseResponse { Success = false, Message = $"Did not find a saved reward for role <@&{request.RoleId}>" };
            this._cybermancyDbContext.Rewards.Remove(reward);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { Success = true, Message = $"Removed {reward.Mention()} reward" };
        }
    }
}

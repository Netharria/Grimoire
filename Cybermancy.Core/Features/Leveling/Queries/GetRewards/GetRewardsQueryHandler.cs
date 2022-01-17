// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Extensions;
using Cybermancy.Core.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Queries.GetRewards
{
    public class GetRewardsQueryHandler : IRequestHandler<GetRewardsQuery, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetRewardsQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<BaseResponse> Handle(GetRewardsQuery request, CancellationToken cancellationToken)
        {
            var rewards = await this._cybermancyDbContext.Rewards
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => $"Level:{x.RewardLevel} Role:{x.Mention()}")
                .ToListAsync(cancellationToken: cancellationToken);
            if (!rewards.Any())
                return new BaseResponse
                {
                    Success = false,
                    Message = "This guild does not have any rewards."
                };
            return new BaseResponse
            {
                Success = true,
                Message = string.Join('\n', rewards)
            };
        }
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Queries.GetRewards;

public class GetRewardsQueryHandler : IRequestHandler<GetRewardsQuery, BaseResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public GetRewardsQueryHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<BaseResponse> Handle(GetRewardsQuery request, CancellationToken cancellationToken)
    {
        var rewards = await this._grimoireDbContext.Rewards
            .AsNoTracking()
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => $"Level:{x.RewardLevel} Role:{x.Mention()} {(x.RewardMessage == null ? "" : $"Reward Message: {x.RewardMessage}")}")
            .ToListAsync(cancellationToken: cancellationToken);
        if (!rewards.Any())
            throw new AnticipatedException("This guild does not have any rewards.");
        return new BaseResponse
        {
            Message = string.Join('\n', rewards)
        };
    }
}

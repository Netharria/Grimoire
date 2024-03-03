// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Queries;

public sealed class GetLevel
{
    public sealed record Query : IRequest<Response>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            var member = await this._grimoireDbContext.Members
            .AsNoTracking()
            .WhereMemberHasId(request.UserId, request.GuildId)
            .Include(x => x.Guild.LevelSettings)
            .Select(Member => new
            {
                Xp = Member.XpHistory.Sum(x => x.Xp),
                Member.Guild.LevelSettings.Base,
                Member.Guild.LevelSettings.Modifier,
                Rewards = Member.Guild.Rewards.OrderBy(reward => reward.RewardLevel)
                .Select(reward => new { reward.RoleId, reward.RewardLevel })
            }).FirstOrDefaultAsync(cancellationToken);

            if (member is null)
                throw new AnticipatedException("That user could not be found.");

            var currentLevel = MemberExtensions.GetLevel(member.Xp, member.Base, member.Modifier);
            var currentLevelXp = MemberExtensions.GetXpNeeded(currentLevel, member.Base, member.Modifier);
            var nextLevelXp = MemberExtensions.GetXpNeeded(currentLevel, member.Base, member.Modifier, 1);

            var nextReward = member.Rewards.FirstOrDefault(reward => reward.RewardLevel > currentLevel);

            return new Response
            {
                UsersXp = member.Xp,
                UsersLevel = currentLevel,
                LevelProgress = member.Xp - currentLevelXp,
                XpForNextLevel = nextLevelXp - currentLevelXp,
                NextRewardLevel = nextReward?.RewardLevel,
                NextRoleRewardId = nextReward?.RoleId,
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public required long UsersXp { get; init; }
        public required  int UsersLevel { get; init; }
        public required long LevelProgress { get; init; }
        public required long XpForNextLevel { get; init; }
        public ulong? NextRoleRewardId { get; init; }
        public int? NextRewardLevel { get; init; }
    }


}


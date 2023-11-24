// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Queries;
public static class GetUserLevelingInfo
{
    public sealed record Query : IQuery<Response?>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
        public required ulong[] RoleIds { get; init; }
    }

    public sealed class Handler(IGrimoireDbContext dbContext) : IQueryHandler<Query, Response?>
    {
        private readonly IGrimoireDbContext _dbContext = dbContext;

        public async ValueTask<Response?> Handle(Query query, CancellationToken cancellationToken)
        {
            var result = await this._dbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(query.UserId, query.GuildId)
                .Select(x => new
                {
                    x.Guild.LevelSettings.ModuleEnabled,
                    x.Guild.LevelSettings.Base,
                    x.Guild.LevelSettings.Modifier,
                    Rewards = x.Guild.Rewards.Select(reward => new { reward.RoleId, reward.RewardLevel }),
                    Response = new Response
                    {
                        Xp = x.XpHistory.Sum(x => x.Xp),
                        IsXpIgnored = x.IsIgnoredMember != null
                        || x.Guild.IgnoredRoles.Any(y => query.RoleIds.Any(z => z == y.RoleId))
                    }
                }).FirstOrDefaultAsync(cancellationToken);
            if (result is null)
                throw new AnticipatedException("Could not find that user. Have they been on the server before?");
            if (!result.ModuleEnabled)
                return null;
            result.Response.Level = MemberExtensions.GetLevel(result.Response.Xp, result.Base, result.Modifier);
            result.Response.EarnedRewards = result.Rewards
                .Where(x => x.RewardLevel <= result.Response.Level)
                .Select(x => x.RoleId)
                .ToArray();
            return result.Response;
        }
    }

    public sealed record Response
    {
        public required long Xp { get; init; }
        public int Level { get; internal set; }
        public required bool IsXpIgnored { get; init; }
        public ulong[] EarnedRewards { get; internal set; } = [];
    }
}

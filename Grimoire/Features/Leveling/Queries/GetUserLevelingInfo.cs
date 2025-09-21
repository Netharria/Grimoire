// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Leveling.Queries;

public static class GetUserLevelingInfo
{
    public sealed record Query : IRequest<Response?>
    {
        public required UserId UserId { get; init; }
        public required GuildId GuildId { get; init; }
        public required IEnumerable<ulong> RoleIds { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Query query, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(query.UserId, query.GuildId)
                .Select(member => new
                {
                    member.Guild.LevelSettings.ModuleEnabled,
                    member.Guild.LevelSettings.Base,
                    member.Guild.LevelSettings.Modifier,
                    Xp = member.XpHistory.Sum(xpHistory => xpHistory.Xp),
                    Rewards = member.Guild.Rewards.Select(reward => new { reward.RoleId, reward.RewardLevel }),
                    Response = new Response
                    {
                        IsXpIgnored = member.IsIgnoredMember != null
                                      || member.Guild.IgnoredRoles
                                          .Any(y => query.RoleIds.Any(z => z == y.RoleId))
                    }
                }).FirstOrDefaultAsync(cancellationToken);
            if (result is null)
                throw new AnticipatedException("Could not find that user. Have they been on the server before?");
            if (!result.ModuleEnabled)
                return null;
            result.Response.Level = MemberExtensions.GetLevel(result.Xp, result.Base, result.Modifier);
            result.Response.EarnedRewards = result.Rewards
                .Where(x => x.RewardLevel <= result.Response.Level)
                .Select(x => x.RoleId);
            return result.Response;
        }
    }

    public sealed record Response
    {
        public int Level { get; internal set; }
        public required bool IsXpIgnored { get; init; }
        public IEnumerable<ulong> EarnedRewards { get; internal set; } = [];
    }
}

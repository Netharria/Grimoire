// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Grimoire.Features.Leveling.Queries;

public sealed class GetLeaderboard
{

    public sealed record Query : IRequest<Response>
    {
        public ulong GuildId { get; init; }
        public ulong? UserId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            if (request.UserId is null)
            {
                var RankedMembers = await this._grimoireDbContext.Members
                .AsNoTracking()
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new { x.UserId, Xp = x.XpHistory.Sum(x => x.Xp) })
                .OrderByDescending(x => x.Xp)
                .Take(15)
                .ToListAsync(cancellationToken: cancellationToken);

                var totalMemberCount = await this._grimoireDbContext.Members
                    .AsNoTracking()
                    .Where(x => x.GuildId == request.GuildId)
                    .CountAsync(cancellationToken: cancellationToken);

                var leaderboardText = new StringBuilder();
                for (var i = 0; i < 15 && i < totalMemberCount; i++)
                    leaderboardText.Append($"**{i + 1}** {UserExtensions.Mention(RankedMembers[i].UserId)} **XP:** {RankedMembers[i].Xp}\n");
                return new Response { LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalMemberCount };
            }
            else
            {
                var RankedMembers = await this._grimoireDbContext.Members
                .AsNoTracking()
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new { x.UserId, Xp = x.XpHistory.Sum(x => x.Xp) })
                .OrderByDescending(x => x.Xp)
                .ToListAsync(cancellationToken: cancellationToken);

                var totalMemberCount = RankedMembers.Count;

                var memberPosition = RankedMembers.FindIndex(x => x.UserId == request.UserId); ;

                if (memberPosition == -1)
                    throw new AnticipatedException("Could not find user on leaderboard.");

                var startIndex = memberPosition - 5 < 0 ? 0 : memberPosition - 5;
                if (startIndex + 15 > totalMemberCount)
                    startIndex = totalMemberCount - 15;
                if (startIndex < 0)
                    startIndex = 0;
                var leaderboardText = new StringBuilder();
                for (var i = 0; i < 15 && startIndex < totalMemberCount; i++)
                {
                    leaderboardText.Append($"**{startIndex + 1}** {UserExtensions.Mention(RankedMembers[startIndex].UserId)} **XP:** {RankedMembers[startIndex].Xp}\n");
                    startIndex++;
                }
                return new Response { LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalMemberCount };
            }
        }
    }

    public sealed record Response : BaseResponse
    {
        public string LeaderboardText { get; init; } = string.Empty;
        public int TotalUserCount { get; init; }
    }
}


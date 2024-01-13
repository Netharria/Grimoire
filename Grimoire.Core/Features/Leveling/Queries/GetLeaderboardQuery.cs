// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Queries;

public sealed class GetLeaderboard
{

    public sealed record Query : IRequest<Response>
    {
        public ulong GuildId { get; init; }
        public ulong? UserId { get; init; }
    }

    public sealed class Handler(IGrimoireDbContext grimoireDbContext) : IRequestHandler<Query, Response>
    {
        private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            var RankedMembers = await this._grimoireDbContext.Members
        .AsNoTracking()
        .Where(x => x.GuildId == request.GuildId)
        .Select(x => new { x.UserId, Xp = x.XpHistory.Sum(x => x.Xp) })
        .OrderByDescending(x => x.Xp)
        .ToListAsync(cancellationToken: cancellationToken);

            var totalMemberCount = RankedMembers.Count;

            var memberPosition = 0;

            if (request.UserId is not null)
                memberPosition = RankedMembers.FindIndex(x => x.UserId == request.UserId);

            if (request.UserId is not null && memberPosition == -1)
                throw new AnticipatedException("Could not find user on leaderboard.");

            if (memberPosition == -1)
                memberPosition++;

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

    public sealed record Response : BaseResponse
    {
        public string LeaderboardText { get; init; } = string.Empty;
        public int TotalUserCount { get; init; }
    }
}


// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Core.Extensions;

namespace Cybermancy.Core.Features.Leveling.Queries.GetLeaderboard
{
    public class GetLeaderboardQueryHandler : IRequestHandler<GetLeaderboardQuery, GetLeaderboardQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetLeaderboardQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<GetLeaderboardQueryResponse> Handle(GetLeaderboardQuery request, CancellationToken cancellationToken)
        {
            var RankedMembers = await this._cybermancyDbContext.Members
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new { x.UserId, Xp = x.XpHistory.Sum(x => x.Xp), Mention = x.User.Mention() })
                .OrderByDescending(x => x.Xp)
                .ToListAsync(cancellationToken: cancellationToken);

            var totalMemberCount = RankedMembers.Count;

            var memberPosition = 0;

            if (request.UserId is not null)
                memberPosition = RankedMembers.FindIndex(x => x.UserId == request.UserId);

            if (request.UserId is not null && memberPosition == -1)
                throw new AnticipatedException("Could not find user on leaderboard.");

            if(memberPosition == -1)
                memberPosition++;

            var startIndex = memberPosition - 5 < 0 ? 0 : memberPosition - 5;
            var leaderboardText = new StringBuilder();

            for (var i = startIndex; i < 15 && i < totalMemberCount; i++)
                leaderboardText.Append($"**{i + 1}** {RankedMembers[i].Mention} **XP:** {RankedMembers[i].Xp}\n");                

            return new GetLeaderboardQueryResponse { LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalMemberCount };
        }
    }
}

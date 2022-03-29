// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Queries.GetLeaderboard
{
    public class GetLeaderboardQueryHandler : IRequestHandler<GetLeaderboardQuery, GetLeaderboardQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetLeaderboardQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<GetLeaderboardQueryResponse> Handle(GetLeaderboardQuery request, CancellationToken cancellationToken)
        {
            var RankedMembers = await this._cybermancyDbContext.Members
                .Where(x => x.GuildId == request.GuildId)
                .OrderByDescending(x => x.Xp)
                .Select(x => new { x.UserId, x.Xp, Mention = x.User.Mention() })
                .ToListAsync(cancellationToken: cancellationToken);

            var totalMemberCount = RankedMembers.Count;

            var memberPosition = 0;

            if (request.UserId is not null)
                memberPosition = RankedMembers.FindIndex(x => x.UserId == request.UserId);

            if (request.UserId is not null && memberPosition == -1)
                return new GetLeaderboardQueryResponse { Success = false, Message = "Could not find user on leaderbaord." };

            if(memberPosition == -1)
                memberPosition++;

            var startIndex = memberPosition - 5 < 0 ? 0 : memberPosition - 5;
            var leaderboardText = new StringBuilder();

            for (var i = startIndex; i < 15 && i < totalMemberCount; i++)
                leaderboardText.Append($"**{i + 1}** {RankedMembers[i].Mention} **XP:** {RankedMembers[i].Xp}\n");                

            return new GetLeaderboardQueryResponse { Success = true, LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalMemberCount };
        }
    }
}

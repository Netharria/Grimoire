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
            var guildRankedUsers = await this._cybermancyDbContext.GuildUsers
                .Where(x => x.GuildId == request.GuildId)
                .OrderByDescending(x => x.Xp)
                .Select(x => new { x.UserId, x.Xp, Mention = x.User.Mention() })
                .ToListAsync(cancellationToken: cancellationToken);

            var totalUserCount = guildRankedUsers.Count;

            var usersPosition = 0;

            if (request.UserId is not null)
                usersPosition = guildRankedUsers.FindIndex(x => x.UserId == request.UserId);

            if (request.UserId is not null && usersPosition == -1)
                return new GetLeaderboardQueryResponse { Success = false, Message = "Could not find user on leaderbaord." };

            if(usersPosition == -1)
                usersPosition++;

            var startIndex = usersPosition - 5 < 0 ? 0 : usersPosition - 5;
            var leaderboardText = new StringBuilder();

            for (var i = startIndex; i < 15 && i < totalUserCount; i++)
                leaderboardText.Append($"**{i + 1}** {guildRankedUsers[i].Mention} **XP:** {guildRankedUsers[i].Xp}\n");                

            return new GetLeaderboardQueryResponse { Success = true, LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalUserCount };
        }
    }
}

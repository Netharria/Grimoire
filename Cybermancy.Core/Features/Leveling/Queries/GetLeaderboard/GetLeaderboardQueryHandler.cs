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
            var totalUserCount = await this._cybermancyDbContext.GuildUsers
                .Where(x => x.GuildId == request.GuildId)
                .CountAsync(cancellationToken: cancellationToken);
            var usersPosition = 1;
            if (request.UserId is not null)
            {
                var usersXp = await this._cybermancyDbContext.GuildUsers
                    .Where(x => x.UserId == request.UserId && x.GuildId == request.GuildId)
                    .Select(x => x.Xp)
                    .FirstOrDefaultAsync(cancellationToken: cancellationToken);

                usersPosition = await this._cybermancyDbContext.GuildUsers
                    .Where(x => x.GuildId == request.GuildId && x.Xp >= usersXp)
                    .CountAsync(cancellationToken: cancellationToken);
            }

            var guildRankedUsers = await this._cybermancyDbContext.GuildUsers
                .GetRankedUsersAsync(request.GuildId, page: usersPosition / 15)
                .Select(x => new { x.UserId, x.Xp, Mention = x.User.Mention() })
                .ToListAsync(cancellationToken: cancellationToken);

            var user = guildRankedUsers.FirstOrDefault(x => x.UserId == request.UserId);
            var userIndex = request.UserId is null || user is null ?  0 :
                guildRankedUsers.IndexOf(user);

            var leaderboardText = new StringBuilder();
            foreach (var (guildUser, i) in guildRankedUsers.Select((value, i) => (value, i)))
                leaderboardText.Append($"**{usersPosition - userIndex + i}** {guildUser.Mention} **XP:** {guildUser.Xp}\n");

            return new GetLeaderboardQueryResponse { Success = true, LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalUserCount };
        }
    }
}

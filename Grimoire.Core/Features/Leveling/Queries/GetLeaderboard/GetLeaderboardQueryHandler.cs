// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Leveling.Queries.GetLeaderboard;

public class GetLeaderboardQueryHandler : IRequestHandler<GetLeaderboardQuery, GetLeaderboardQueryResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public GetLeaderboardQueryHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<GetLeaderboardQueryResponse> Handle(GetLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var RankedMembers = await this._grimoireDbContext.Members
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

        if (memberPosition == -1)
            memberPosition++;

        var startIndex = memberPosition - 5 < 0 ? 0 : memberPosition - 5;
        var leaderboardText = new StringBuilder();

        for (var i = 0; i < 15 && startIndex < totalMemberCount; i++)
        {
            leaderboardText.Append($"**{startIndex + 1}** {RankedMembers[startIndex].Mention} **XP:** {RankedMembers[startIndex].Xp}\n");
            startIndex++;
        }
            

        return new GetLeaderboardQueryResponse { LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalMemberCount };
    }
}

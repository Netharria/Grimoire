// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries.GetExpiredMutes;

public class GetExpiredMutesQueryHandler : IQueryHandler<GetExpiredMutesQuery, IList<GetExpiredMutesQueryResponse>>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public GetExpiredMutesQueryHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<IList<GetExpiredMutesQueryResponse>> Handle(GetExpiredMutesQuery query, CancellationToken cancellationToken)
    {
        var response = await this._grimoireDbContext.Mutes.Where(x => x.EndTime < DateTimeOffset.UtcNow)
            .Where(x => x.Guild.ModerationSettings.MuteRole != null)
            .Select(x => new
            {
                x.UserId,
                x.GuildId,
                x.Guild.ModerationSettings.MuteRole,
                x.Guild.ModChannelLog
            }).ToArrayAsync(cancellationToken);

        return response
            .Where(x => x.MuteRole is not null)
            .Select(x => new GetExpiredMutesQueryResponse
            {
                UserId = x.UserId,
                GuildId = x.GuildId,
                MuteRole = x.MuteRole!.Value,
                LogChannelId = x.ModChannelLog
            }).ToArray();
    }

}

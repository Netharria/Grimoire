// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries;

public sealed record GetExpiredMutesQuery : IQuery<IList<GetExpiredMutesQueryResponse>> { }

public sealed class GetExpiredMutesQueryHandler(IGrimoireDbContext grimoireDbContext) : IQueryHandler<GetExpiredMutesQuery, IList<GetExpiredMutesQueryResponse>>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<IList<GetExpiredMutesQueryResponse>> Handle(GetExpiredMutesQuery query, CancellationToken cancellationToken)
    {
        var response = await this._grimoireDbContext.Mutes
            .AsNoTracking()
            .Where(x => x.EndTime < DateTimeOffset.UtcNow)
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

public sealed record GetExpiredMutesQueryResponse : BaseResponse
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public ulong MuteRole { get; init; }
}

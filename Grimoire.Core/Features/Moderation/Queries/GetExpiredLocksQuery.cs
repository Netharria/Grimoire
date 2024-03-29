// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries;

public sealed record GetExpiredLocksQuery : IQuery<IEnumerable<GetExpiredLocksQueryResponse>> { }

public sealed class GetExpiredLocksQueryHandler(GrimoireDbContext grimoireDbContext) : IQueryHandler<GetExpiredLocksQuery, IEnumerable<GetExpiredLocksQueryResponse>>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<IEnumerable<GetExpiredLocksQueryResponse>> Handle(GetExpiredLocksQuery query, CancellationToken cancellationToken)
     => await this._grimoireDbContext.Locks
            .AsNoTracking()
            .Where(x => x.EndTime < DateTimeOffset.UtcNow)
            .Select(x => new GetExpiredLocksQueryResponse
            {
                ChannelId = x.ChannelId,
                GuildId = x.GuildId,
                PreviouslyAllowed = x.PreviouslyAllowed,
                PreviouslyDenied = x.PreviouslyDenied,
                LogChannelId = x.Guild.ModChannelLog
            }).ToArrayAsync(cancellationToken);
}

public sealed record GetExpiredLocksQueryResponse : BaseResponse
{
    public ulong ChannelId { get; init; }
    public ulong GuildId { get; init; }
    public long PreviouslyAllowed { get; init; }
    public long PreviouslyDenied { get; init; }
}

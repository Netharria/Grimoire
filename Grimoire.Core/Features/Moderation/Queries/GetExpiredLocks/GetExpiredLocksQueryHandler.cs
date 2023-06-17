// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries.GetExpiredLocks
{
    public class GetExpiredLocksQueryHandler : IQueryHandler<GetExpiredLocksQuery, IEnumerable<GetExpiredLocksQueryResponse>>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public GetExpiredLocksQueryHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<IEnumerable<GetExpiredLocksQueryResponse>> Handle(GetExpiredLocksQuery query, CancellationToken cancellationToken)
         => await this._grimoireDbContext.Locks.Where(x => x.EndTime < DateTimeOffset.UtcNow)
                .Select(x => new GetExpiredLocksQueryResponse
                {
                    ChannelId = x.ChannelId,
                    GuildId = x.GuildId,
                    PreviouslyAllowed = x.PreviouslyAllowed,
                    PreviouslyDenied = x.PreviouslyDenied,
                    LogChannelId = x.Guild.ModChannelLog
                }).ToArrayAsync(cancellationToken);
    }
}
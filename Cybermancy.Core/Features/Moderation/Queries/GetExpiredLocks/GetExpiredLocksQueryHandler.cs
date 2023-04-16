// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cybermancy.Core.Features.Moderation.Queries.GetExpiredLocks
{
    public class GetExpiredLocksQueryHandler : IQueryHandler<GetExpiredLocksQuery, IEnumerable<GetExpiredLocksQueryResponse>>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetExpiredLocksQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<IEnumerable<GetExpiredLocksQueryResponse>> Handle(GetExpiredLocksQuery query, CancellationToken cancellationToken)
         => await this._cybermancyDbContext.Locks.Where(x => x.EndTime < DateTimeOffset.UtcNow)
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

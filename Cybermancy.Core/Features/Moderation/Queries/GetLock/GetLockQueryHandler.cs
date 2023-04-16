// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Moderation.Queries.GetLock
{
    public class GetLockQueryHandler : IQueryHandler<GetLockQuery, bool>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetLockQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<bool> Handle(GetLockQuery query, CancellationToken cancellationToken)
            => await this._cybermancyDbContext.Locks
                .AnyAsync(x => x.ChannelId == query.ChannelId && x.GuildId == query.GuildId, cancellationToken);
    }
}

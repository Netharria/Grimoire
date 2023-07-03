// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries.GetLock;

public class GetLockQueryHandler : IQueryHandler<GetLockQuery, bool>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public GetLockQueryHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<bool> Handle(GetLockQuery query, CancellationToken cancellationToken)
        => await this._grimoireDbContext.Locks
            .AsNoTracking()
            .AnyAsync(x => x.ChannelId == query.ChannelId && x.GuildId == query.GuildId, cancellationToken);
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Queries;

public sealed record GetLockQuery : IQuery<bool>
{
    public ulong ChannelId { get; init; }
    public ulong GuildId { get; init; }
}

public sealed class GetLockQueryHandler(GrimoireDbContext grimoireDbContext) : IQueryHandler<GetLockQuery, bool>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<bool> Handle(GetLockQuery query, CancellationToken cancellationToken)
        => await this._grimoireDbContext.Locks
            .AsNoTracking()
            .AnyAsync(x => x.ChannelId == query.ChannelId && x.GuildId == query.GuildId, cancellationToken);
}

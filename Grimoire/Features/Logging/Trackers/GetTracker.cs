// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Logging.Trackers;

public sealed class GetTracker
{
    public sealed record Query : IRequest<Response?>
    {
        public ulong UserId { get; init; }
        public GuildId GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Query request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Trackers
                .AsNoTracking()
                .WhereMemberHasId(request.UserId, request.GuildId)
                .Select(x => new Response { TrackerChannelId = x.LogChannelId })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    public sealed record Response
    {
        public ulong TrackerChannelId { get; init; }
    }
}

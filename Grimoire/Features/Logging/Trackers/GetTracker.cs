// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Logging.Trackers;

public sealed class GetTracker
{
    public sealed record Query : IRequest<Response?>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, Response?>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response?> Handle(Query request, CancellationToken cancellationToken)
            => await this._grimoireDbContext.Trackers
            .AsNoTracking()
            .WhereMemberHasId(request.UserId, request.GuildId)
            .Select(x => new Response
            {
                TrackerChannelId = x.LogChannelId
            }).FirstOrDefaultAsync(cancellationToken);

    }

    public sealed record Response : BaseResponse
    {
        public ulong TrackerChannelId { get; init; }
    }
}



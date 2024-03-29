// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.MessageLogging.Queries;

public sealed record GetTrackerQuery : IRequest<GetTrackerQueryResponse?>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
}

public sealed class GetTrackerQueryHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<GetTrackerQuery, GetTrackerQueryResponse?>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<GetTrackerQueryResponse?> Handle(GetTrackerQuery request, CancellationToken cancellationToken)
        => await this._grimoireDbContext.Trackers
        .AsNoTracking()
        .WhereMemberHasId(request.UserId, request.GuildId)
        .Select(x => new GetTrackerQueryResponse
        {
            TrackerChannelId = x.LogChannelId
        }).FirstOrDefaultAsync(cancellationToken);

}

public sealed record GetTrackerQueryResponse : BaseResponse
{
    public ulong TrackerChannelId { get; init; }
}

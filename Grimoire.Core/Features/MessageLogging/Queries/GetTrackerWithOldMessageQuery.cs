// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.MessageLogging.Queries;

public sealed record GetTrackerWithOldMessageQuery : IRequest<GetTrackerWithOldMessageQueryResponse?>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public ulong MessageId { get; init; }
}

public class GetTrackerWithOldMessageQueryHandler(IGrimoireDbContext grimoireDbContext) : IRequestHandler<GetTrackerWithOldMessageQuery, GetTrackerWithOldMessageQueryResponse?>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<GetTrackerWithOldMessageQueryResponse?> Handle(GetTrackerWithOldMessageQuery request, CancellationToken cancellationToken)
        => await this._grimoireDbContext.Trackers
        .AsNoTracking()
        .WhereMemberHasId(request.UserId, request.GuildId)
        .Select(x => new GetTrackerWithOldMessageQueryResponse
        {
            TrackerChannelId = x.LogChannelId,
            OldMessageContent = x.Member.Messages
                .Where(x => x.Id == request.MessageId)
                .Select(x => x.MessageHistory
                    .Where(x => x.Action != MessageAction.Deleted
                        && x.TimeStamp < DateTime.UtcNow.AddSeconds(-1))
                    .OrderByDescending(x => x.TimeStamp)
                    .First().MessageContent)
                .First()
        }).FirstOrDefaultAsync(cancellationToken);
}

public sealed record GetTrackerWithOldMessageQueryResponse : BaseResponse
{
    public ulong TrackerChannelId { get; init; }
    public string OldMessageContent { get; init; } = string.Empty;
}

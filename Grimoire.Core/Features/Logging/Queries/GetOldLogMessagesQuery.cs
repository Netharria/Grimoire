// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Queries;

public sealed record GetOldLogMessagesQuery : IRequest<IEnumerable<GetOldLogMessagesQueryResponse>> { }

public class GetOldLogMessagesQueryHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<GetOldLogMessagesQuery, IEnumerable<GetOldLogMessagesQueryResponse>>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<IEnumerable<GetOldLogMessagesQueryResponse>> Handle(GetOldLogMessagesQuery request, CancellationToken cancellationToken)
    {
        var oldDate = DateTime.UtcNow - TimeSpan.FromDays(30);
        return await this._grimoireDbContext.OldLogMessages
            .Where(x => x.CreatedAt < oldDate)
            .GroupBy(x => new { x.ChannelId, x.GuildId })
            .Select(x => new GetOldLogMessagesQueryResponse
            {
                ChannelId = x.Key.ChannelId,
                GuildId = x.Key.GuildId,
                MessageIds = x.Select(x => x.Id).ToArray()
            }).ToArrayAsync(cancellationToken: cancellationToken);
    }
}

public sealed record GetOldLogMessagesQueryResponse : BaseResponse
{
    public ulong ChannelId { get; init; }
    public ulong GuildId { get; init; }
    public ulong[] MessageIds { get; init; } = [];
}

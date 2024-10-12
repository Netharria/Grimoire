// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.LogCleanup.Queries;

public sealed class GetOldLogMessages
{
    public sealed record Query : IRequest<IEnumerable<Response>> { }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, IEnumerable<Response>>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<IEnumerable<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var oldDate = DateTime.UtcNow - TimeSpan.FromDays(30);
            return await this._grimoireDbContext.OldLogMessages
                .Where(x => x.CreatedAt < oldDate)
                .GroupBy(x => new { x.ChannelId, x.GuildId })
                .Select(x => new Response
                {
                    ChannelId = x.Key.ChannelId,
                    GuildId = x.Key.GuildId,
                    MessageIds = x.Select(x => x.Id).ToArray()
                }).ToArrayAsync(cancellationToken: cancellationToken);
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong ChannelId { get; init; }
        public ulong GuildId { get; init; }
        public ulong[] MessageIds { get; init; } = [];
    }

}

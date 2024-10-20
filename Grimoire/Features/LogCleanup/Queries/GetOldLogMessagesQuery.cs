// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Grimoire.Features.LogCleanup.Queries;

public sealed class GetOldLogMessages
{
    public sealed record Query : IStreamRequest<Response> { }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IStreamRequestHandler<Query, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async IAsyncEnumerable<Response> Handle(Query request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var oldDate = DateTime.UtcNow - TimeSpan.FromDays(30);
            await foreach(var item in this._grimoireDbContext.OldLogMessages
                .Where(x => x.CreatedAt < oldDate)
                .GroupBy(x => new { x.ChannelId, x.GuildId })
                .Select(x => new Response
                {
                    ChannelId = x.Key.ChannelId,
                    GuildId = x.Key.GuildId,
                    MessageIds = x.Select(x => x.Id).ToArray()
                }).AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong ChannelId { get; init; }
        public ulong GuildId { get; init; }
        public ulong[] MessageIds { get; init; } = [];
    }

}

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
    public sealed record Query : IStreamRequest<Response>
    {
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IStreamRequestHandler<Query, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async IAsyncEnumerable<Response> Handle(Query request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var oldDate = DateTime.UtcNow - TimeSpan.FromDays(30);
            await foreach (var item in dbContext.OldLogMessages
                               .Where(oldLogMessage => oldLogMessage.CreatedAt < oldDate)
                               .GroupBy(oldLogMessage => new { oldLogMessage.ChannelId, oldLogMessage.GuildId })
                               .Select(oldLogMessages => new Response
                               {
                                   ChannelId = oldLogMessages.Key.ChannelId,
                                   GuildId = oldLogMessages.Key.GuildId,
                                   MessageIds = oldLogMessages.Select(x => x.Id).ToArray()
                               }).AsAsyncEnumerable().WithCancellation(cancellationToken))
                yield return item;
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong ChannelId { get; init; }
        public ulong GuildId { get; init; }
        public ulong[] MessageIds { get; init; } = [];
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.MessageLogging.Queries;
public sealed class GetMessageLogOverrides
{
    public sealed record Query : IQuery<List<Response>>
    {
        public required ulong GuildId { get; init; }
    }
    public sealed record Response
    {
        public required ulong ChannelId { get; init; }
        public required MessageLogOverrideOption ChannelOption { get; init; }
    }

    public sealed class Handler(GrimoireDbContext dbContext) : IQueryHandler<Query, List<Response>>
    {
        private readonly GrimoireDbContext _dbContext = dbContext;

        public async ValueTask<List<Response>> Handle(Query query, CancellationToken cancellationToken)
            => await this._dbContext.MessagesLogChannelOverrides
            .AsNoTracking()
            .Where(x => x.GuildId == query.GuildId)
            .Select(x => new Response
            {
                ChannelId = x.ChannelId,
                ChannelOption = x.ChannelOption
            })
            .ToListAsync(cancellationToken);
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Queries;
public sealed class GetUserCommandChannel
{
    public sealed record Query : IQuery<Response>
    {
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext dbContext) : IQueryHandler<Query, Response>
    {
        private readonly GrimoireDbContext _dbContext = dbContext;

        public async ValueTask<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var result = await this._dbContext.Guilds
                .Where(x => x.Id == query.GuildId)
                .Select(x => x.UserCommandChannelId
                ).FirstOrDefaultAsync(cancellationToken);

            return new Response
            {
                UserCommandChannelId = result
            };
        }
    }

    public sealed record Response
    {
        public ulong? UserCommandChannelId { get; init; }
    }
}

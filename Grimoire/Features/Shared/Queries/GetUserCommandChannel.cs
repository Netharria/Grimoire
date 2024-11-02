// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Queries;

public sealed class GetUserCommandChannel
{
    public sealed record Query : IRequest<Response?>
    {
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<Query, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Query query, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Guilds
                .Where(guild => guild.Id == query.GuildId)
                .Select(guild =>  new Response
                    {
                        UserCommandChannelId = guild.UserCommandChannelId
                    }
                ).FirstOrDefaultAsync(cancellationToken);
        }
    }

    public sealed record Response
    {
        public ulong? UserCommandChannelId { get; init; }
    }
}

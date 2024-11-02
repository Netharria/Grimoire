// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Logging.MessageLogging;

public sealed class GetMessageAuthor
{
    public sealed record Query : IRequest<ulong?>
    {
        public ulong MessageId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<Query, ulong?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<ulong?> Handle(Query query, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Messages
                .AsNoTracking()
                .Where(message => message.Id == query.MessageId)
                .Select(message => message.UserId)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}

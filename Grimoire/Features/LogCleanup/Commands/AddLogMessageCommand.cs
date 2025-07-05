// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.LogCleanup.Commands;

public sealed class AddLogMessage
{
    public sealed record Command : IRequest
    {
        public required ulong ChannelId { get; init; }
        public required ulong MessageId { get; init; }
        public required ulong GuildId { get; init; }
    }

    public sealed class AddLogMessageCommandHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var logMessage = new OldLogMessage
            {
                ChannelId = command.ChannelId, GuildId = command.GuildId, Id = command.MessageId
            };
            await dbContext.OldLogMessages.AddAsync(logMessage, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

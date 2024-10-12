// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.LogCleanup.Commands;

public sealed class AddLogMessage
{
    public sealed record Command : ICommand
    {
        public ulong ChannelId { get; init; }
        public ulong MessageId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class AddLogMessageCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Command>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            var logMessage = new OldLogMessage
            {
                ChannelId = command.ChannelId,
                GuildId = command.GuildId,
                Id = command.MessageId
            };
            await this._grimoireDbContext.OldLogMessages.AddAsync(logMessage, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }

}


// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.LogCleanup.Commands;

public sealed record DeleteMessageResult
{
    public bool WasSuccessful { get; init; }
    public ulong MessageId { get; init; }
}

public sealed class DeleteOldLogMessages
{
    public sealed record Command : ICommand
    {
        public DeleteMessageResult[] DeletedOldLogMessageIds { get; init; } = [];
    }

    public sealed class Handler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<Command>
    {
        private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            var successMessages = command.DeletedOldLogMessageIds
            .Where(x => x.WasSuccessful == true)
            .Select(x => x.MessageId)
            .ToArray();

            await this._grimoireDbContext.OldLogMessages
                .WhereIdsAre(successMessages)
                .ExecuteDeleteAsync(cancellationToken);

            var erroredMessages = command.DeletedOldLogMessageIds
            .Where(x => x.WasSuccessful == false)
            .Select(x => x.MessageId)
            .ToArray();

            if (erroredMessages.Length != 0)
            {
                await this._grimoireDbContext.OldLogMessages
                .WhereIdsAre(erroredMessages)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.TimesTried, p => p.TimesTried + 1), cancellationToken);

                await this._grimoireDbContext.OldLogMessages
                    .Where(x => x.TimesTried >= 3)
                    .ExecuteDeleteAsync(cancellationToken);
            }
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }

}

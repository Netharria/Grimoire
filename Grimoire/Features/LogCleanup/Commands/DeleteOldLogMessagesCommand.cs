// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.LogCleanup.Commands;

public readonly struct DeleteMessageResult
{
    public bool WasSuccessful { get; init; }
    public ulong MessageId { get; init; }
}

public sealed class DeleteOldLogMessages
{
    public sealed record Command : IRequest
    {
        public required IEnumerable<DeleteMessageResult> DeletedOldLogMessageIds { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Command>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            var successMessages = command.DeletedOldLogMessageIds
                .Where(x => x.WasSuccessful)
                .Select(x => x.MessageId)
                .ToArray();

            if(successMessages.Length != 0)
                await this._grimoireDbContext.OldLogMessages
                    .WhereIdsAre(successMessages)
                    .ExecuteDeleteAsync(cancellationToken);


            var erroredMessages = command.DeletedOldLogMessageIds
                .Where(x => !x.WasSuccessful)
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
        }
    }

}

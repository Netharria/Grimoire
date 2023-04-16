// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.DeleteOldLogMessages
{
    public class DeleteOldLogMessagesCommandHandler : ICommandHandler<DeleteOldLogMessagesCommand>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public DeleteOldLogMessagesCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<Unit> Handle(DeleteOldLogMessagesCommand command, CancellationToken cancellationToken)
        {
            var messages = this._grimoireDbContext.OldLogMessages
                .Where(oldLogMessage =>
                    command.DeletedOldLogMessageIds
                    .Any(messageId => messageId == oldLogMessage.Id));
            this._grimoireDbContext.OldLogMessages.RemoveRange(messages);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

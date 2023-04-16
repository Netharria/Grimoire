// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteOldMessages
{
    public class DeleteOldMessagesCommandHandler : ICommandHandler<DeleteOldMessagesCommand>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public DeleteOldMessagesCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<Unit> Handle(DeleteOldMessagesCommand command, CancellationToken cancellationToken)
        {
            var oldDate = DateTime.UtcNow - TimeSpan.FromDays(31);
            var oldMessages = await this._grimoireDbContext.Messages
                .Where(x => x.CreatedTimestamp  == oldDate)
                .ToArrayAsync(cancellationToken: cancellationToken);
            this._grimoireDbContext.Messages.RemoveRange(oldMessages);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

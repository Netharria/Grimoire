// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteOldMessages
{
    public class DeleteOldMessagesCommandHandler : ICommandHandler<DeleteOldMessagesCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public DeleteOldMessagesCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(DeleteOldMessagesCommand command, CancellationToken cancellationToken)
        {
            var oldDate = DateTime.UtcNow - TimeSpan.FromDays(31);
            var oldMessages = await this._cybermancyDbContext.Messages
                .Where(x => x.CreatedTimestamp  == oldDate)
                .ToArrayAsync(cancellationToken: cancellationToken);
            this._cybermancyDbContext.Messages.RemoveRange(oldMessages);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

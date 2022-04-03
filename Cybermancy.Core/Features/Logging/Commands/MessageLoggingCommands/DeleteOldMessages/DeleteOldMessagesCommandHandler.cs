// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using MediatR;

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteOldMessages
{
    public class DeleteOldMessagesCommandHandler : IRequestHandler<DeleteOldMessagesCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public DeleteOldMessagesCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(DeleteOldMessagesCommand request, CancellationToken cancellationToken)
        {
            var oldDate = DateTime.UtcNow - TimeSpan.FromDays(31);
            var oldMessages = this._cybermancyDbContext.Messages
                .Where(x => x.CreatedTimestamp  == oldDate);
            this._cybermancyDbContext.Messages.RemoveRange(oldMessages);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using MediatR;

namespace Cybermancy.Core.Features.Logging.Commands.DeleteOldLogMessages
{
    public class DeleteOldLogMessagesCommandHandler : IRequestHandler<DeleteOldLogMessagesCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public DeleteOldLogMessagesCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(DeleteOldLogMessagesCommand request, CancellationToken cancellationToken)
        {
            var messages = this._cybermancyDbContext.OldLogMessages
                .Where(oldLogMessage =>
                    request.DeletedOldLogMessageIds
                    .Any(messageId => messageId == oldLogMessage.Id));
            this._cybermancyDbContext.OldLogMessages.RemoveRange(messages);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

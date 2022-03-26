// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using MediatR;

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.MarkMessageAsDeleted
{
    public class MarkMessageAsDeletedCommandHandler : IRequestHandler<MarkMessageAsDeletedCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public MarkMessageAsDeletedCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(MarkMessageAsDeletedCommand request, CancellationToken cancellationToken)
        {
            var messages = this._cybermancyDbContext.Messages
                .Where(x => request.Ids.Contains(x.Id))
                .Select(x =>
                new MessageHistory
                {
                    MessageId = x.Id,
                    Action = MessageAction.Deleted,
                    GuildId = request.GuildId
                });
            await this._cybermancyDbContext.MessageHistory.AddRangeAsync(messages, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

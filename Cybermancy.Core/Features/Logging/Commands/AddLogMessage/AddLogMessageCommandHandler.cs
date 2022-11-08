// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using Mediator;

namespace Cybermancy.Core.Features.Logging.Commands.AddLogMessage
{
    public class AddLogMessageCommandHandler : ICommandHandler<AddLogMessageCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AddLogMessageCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(AddLogMessageCommand request, CancellationToken cancellationToken)
        {
            var logMessage = new OldLogMessage
            {
                ChannelId = request.ChannelId,
                GuildId = request.GuildId,
                Id = request.MessageId
            };
            await this._cybermancyDbContext.OldLogMessages.AddAsync(logMessage, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

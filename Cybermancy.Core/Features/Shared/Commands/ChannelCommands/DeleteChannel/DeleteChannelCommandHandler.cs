// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain;
using Mediator;

namespace Cybermancy.Core.Features.Shared.Commands.ChannelCommands.DeleteChannel
{
    public class DeleteChannelCommandHandler : ICommandHandler<DeleteChannelCommand>
    {
        private readonly CybermancyDbContext _cybermancyDbContext;

        public DeleteChannelCommandHandler(CybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(DeleteChannelCommand command, CancellationToken cancellationToken)
        {
            this._cybermancyDbContext.Channels.Remove(new Channel { Id = command.ChannelId });
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

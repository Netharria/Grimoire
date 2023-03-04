// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain;
using Mediator;

namespace Cybermancy.Core.Features.Shared.Commands.ChannelCommands.AddChannel
{
    public class AddChannelCommandHandler : ICommandHandler<AddChannelCommand>
    {
        private readonly CybermancyDbContext _cybermancyDbContext;

        public AddChannelCommandHandler(CybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(AddChannelCommand command, CancellationToken cancellationToken)
        {
            await this._cybermancyDbContext.Channels.AddAsync(new Channel
                {
                    Id = command.ChannelId,
                    GuildId = command.GuildId
                }, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

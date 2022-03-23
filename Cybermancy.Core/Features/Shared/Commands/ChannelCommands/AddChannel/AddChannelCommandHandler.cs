// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain;
using MediatR;

namespace Cybermancy.Core.Features.Shared.Commands.ChannelCommands.AddChannel
{
    public class AddChannelCommandHandler : IRequestHandler<AddChannelCommand>
    {
        private readonly CybermancyDbContext _cybermancyDbContext;

        public AddChannelCommandHandler(CybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(AddChannelCommand request, CancellationToken cancellationToken)
        {
            await this._cybermancyDbContext.Channels.AddAsync(new Channel
                {
                    Id = request.ChannelId,
                    Name = request.ChannelName,
                    GuildId = request.GuildId
                }, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

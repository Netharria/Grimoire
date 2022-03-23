// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Shared.Commands.ChannelCommands.UpdateChannel
{
    public class UpdateChannelCommandHandler : IRequestHandler<UpdateChannelCommand>
    {
        private readonly CybermancyDbContext _cybermancyDbContext;

        public UpdateChannelCommandHandler(CybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(UpdateChannelCommand request, CancellationToken cancellationToken)
        {
            var channel = await this._cybermancyDbContext.Channels.SingleAsync(x => x.Id == request.ChannelId, cancellationToken: cancellationToken);
            channel.Name = request.ChannelName;
            this._cybermancyDbContext.Channels.Update(channel);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using Mediator;

namespace Cybermancy.Core.Features.Moderation.Commands.BanComands.PublishBan
{
    public class PublishBanCommandHandler : ICommandHandler<PublishBanCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public PublishBanCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(PublishBanCommand command, CancellationToken cancellationToken)
        {
            await _cybermancyDbContext.PublishedMessages.AddAsync(new PublishedMessage
            {
                MessageId = command.MessageId,
                SinId = command.SinId,
                PublishType = command.PublishType,
            }, cancellationToken);
            await _cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new Unit();
        }
    }
}

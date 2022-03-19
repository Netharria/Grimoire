// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using MediatR;

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.AddMessage
{
    public class AddMessageHandler : IRequestHandler<AddMessageCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AddMessageHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(AddMessageCommand request, CancellationToken cancellationToken)
        {
            var message = new Message
            {
                Id = request.MessageId,
                AuthorId = request.AuthorId,
                Attachments = request.Attachments
                    .Select(x => new Attachment{ MessageId = request.MessageId, AttachmentUrl = x})
                    .ToArray(),
                ChannelId = request.ChannelId,
                Content = request.MessageContent,
                CreatedTimestamp = request.CreatedTimestamp,
                ReferencedMessageId = request.ReferencedMessageId
            };
            await _cybermancyDbContext.Messages.AddAsync(message, cancellationToken);
            await _cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

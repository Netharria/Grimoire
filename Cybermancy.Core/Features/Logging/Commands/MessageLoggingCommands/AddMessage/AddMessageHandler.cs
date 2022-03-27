// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
            if (!await this._cybermancyDbContext.Guilds
                .Where(x => x.Id == request.GuildId)
                .AnyAsync(x => x.LogSettings.IsLoggingEnabled,
                cancellationToken))
                return Unit.Value;
            var message = new Message
            {
                Id = request.MessageId,
                AuthorId = request.AuthorId,
                Attachments = request.Attachments
                    .Select(x => new Attachment{ MessageId = request.MessageId, AttachmentUrl = x})
                    .ToArray(),
                ChannelId = request.ChannelId,
                CreatedTimestamp = request.CreatedTimestamp,
                ReferencedMessageId = request.ReferencedMessageId,
                GuildId = request.GuildId,
                MessageHistory = new List<MessageHistory>{
                    new MessageHistory
                    {
                        MessageId = request.MessageId,
                        MessageContent = request.MessageContent,
                        GuildId = request.GuildId,
                        Action = MessageAction.Created
                    }
                }
            };
            await this._cybermancyDbContext.Messages.AddAsync(message, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.AddMessage
{
    public class AddMessageCommandHandler : ICommandHandler<AddMessageCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AddMessageCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(AddMessageCommand request, CancellationToken cancellationToken)
        {
            if (!await this._cybermancyDbContext.Guilds
                .WhereIdIs(request.GuildId)
                .AnyAsync(x => x.LogSettings.ModuleEnabled,
                cancellationToken))
                return Unit.Value;
            var message = new Message
            {
                Id = request.MessageId,
                UserId = request.UserId,
                Attachments = request.Attachments
                    .Select(x =>
                        new Attachment
                        {
                            Id = x.Id,
                            MessageId = request.MessageId,
                            FileName = x.FileName,
                        })
                    .ToArray(),
                ChannelId = request.ChannelId,
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

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Features.Shared.SharedDtos;
using Cybermancy.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteMessage
{
    public class DeleteMessageCommandHandler : ICommandHandler<DeleteMessageCommand, DeleteMessageCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public DeleteMessageCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<DeleteMessageCommandResponse> Handle(DeleteMessageCommand command, CancellationToken cancellationToken)
        {
            var message = await this._cybermancyDbContext.Messages
                .WhereIdIs(command.Id)
                .WhereLoggingIsEnabled()
                .Select(x => new DeleteMessageCommandResponse
                {
                    LoggingChannel = x.Guild.LogSettings.DeleteChannelLogId,
                    UserId = x.UserId,
                    MessageContent = x.MessageHistory
                        .OrderByDescending(x => x.TimeStamp)
                        .First(y => y.Action != MessageAction.Deleted)
                        .MessageContent,
                    Attachments = x.Attachments
                        .Select(x => new AttachmentDto
                        {
                            Id = x.Id,
                            FileName = x.FileName,
                        })
                        .ToArray(),
                    Success = true

                }).FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (message is null)
                return new DeleteMessageCommandResponse { Success = false };
            await this._cybermancyDbContext.MessageHistory.AddAsync(new MessageHistory
            {
                MessageId = command.Id,
                Action = MessageAction.Deleted,
                GuildId = command.GuildId,
                DeletedByModeratorId = command.DeletedByModerator
            }, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return message;
        }
    }
}

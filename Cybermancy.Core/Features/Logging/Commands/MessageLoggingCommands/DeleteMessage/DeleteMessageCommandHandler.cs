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

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteMessage
{
    public class DeleteMessageCommandHandler : IRequestHandler<DeleteMessageCommand, DeleteMessageCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public DeleteMessageCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<DeleteMessageCommandResponse> Handle(DeleteMessageCommand request, CancellationToken cancellationToken)
        {
            if (await this._cybermancyDbContext.Guilds
                .Where(x => x.Id == request.GuildId)
                .AnyAsync(x => !x.LogSettings.IsLoggingEnabled,
                cancellationToken: cancellationToken))
                return new DeleteMessageCommandResponse { Success = false };
            var message = await this._cybermancyDbContext.Messages
                .Where(x => x.Id == request.Id)
                .Select(x => new DeleteMessageCommandResponse
                {
                    LoggingChannel = x.Guild.LogSettings.DeleteChannelLogId,
                    AuthorId = x.AuthorId,
                    MessageContent = x.MessageHistory
                        .First(y => y.Action != MessageAction.Deleted)
                        .MessageContent,
                    AttachmentUrls = x.Attachments
                        .Select(x => x.AttachmentUrl)
                        .ToArray(),
                    Success = true

                }).FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (message is null)
                return new DeleteMessageCommandResponse { Success = false };
            await this._cybermancyDbContext.MessageHistory.AddAsync(new MessageHistory
            {
                MessageId = request.Id,
                Action = MessageAction.Deleted,
                GuildId = request.GuildId,
                DeletedByModeratorId = request.DeletedByModerator
            }, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return message;
        }
    }
}

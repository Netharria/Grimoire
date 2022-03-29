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
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.BulkDeleteMessages
{
    public class BulkDeleteMessageCommandHandler : IRequestHandler<BulkDeleteMessageCommand, BulkDeleteMessageCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public BulkDeleteMessageCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<BulkDeleteMessageCommandResponse> Handle(BulkDeleteMessageCommand request, CancellationToken cancellationToken)
        {
            var messages = await this._cybermancyDbContext.Messages
                .WhereIdsAre(request.Ids)
                .WhereLoggingIsEnabled()
                .Select(x => new
                {
                    Message = new MessageDto
                    {
                        MessageId = x.Id,
                        UserId = x.UserId,
                        MessageContent = x.MessageHistory
                        .OrderByDescending(x => x.TimeStamp)
                        .First(x => x.Action != MessageAction.Deleted)
                        .MessageContent,
                        AttachmentUrls = x.Attachments
                        .Select(x => x.AttachmentUrl)
                        .ToArray(),
                        ChannelId = x.ChannelId
                    },
                    BulkDeleteLogId = x.Guild.LogSettings.BulkDeleteChannelLogId
                }

                ).ToArrayAsync(cancellationToken: cancellationToken);
            if (!messages.Any())
                return new BulkDeleteMessageCommandResponse { Success = false };

            var messageHistory = messages.Select(x => new MessageHistory
            {
                MessageId = x.Message.MessageId,
                Action = MessageAction.Deleted,
                GuildId = request.GuildId
            });

            await this._cybermancyDbContext.MessageHistory.AddRangeAsync(messageHistory, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new BulkDeleteMessageCommandResponse
            {
                BulkDeleteLogChannelId = messages.First()?.BulkDeleteLogId,
                Messages = messages.Select(x => x.Message).ToArray(),
                Success = true
            };
        }
    }
}

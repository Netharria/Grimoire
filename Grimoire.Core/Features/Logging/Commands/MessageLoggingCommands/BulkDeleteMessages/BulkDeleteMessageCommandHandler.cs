// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands.BulkDeleteMessages
{
    public class BulkDeleteMessageCommandHandler : ICommandHandler<BulkDeleteMessageCommand, BulkDeleteMessageCommandResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public BulkDeleteMessageCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<BulkDeleteMessageCommandResponse> Handle(BulkDeleteMessageCommand command, CancellationToken cancellationToken)
        {
            var messages = await this._grimoireDbContext.Messages
                .WhereIdsAre(command.Ids)
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
                        Attachments = x.Attachments
                        .Select(x => new AttachmentDto
                        {
                            Id = x.Id,
                            FileName = x.FileName
                        })
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
                GuildId = command.GuildId
            });

            await this._grimoireDbContext.MessageHistory.AddRangeAsync(messageHistory, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new BulkDeleteMessageCommandResponse
            {
                BulkDeleteLogChannelId = messages.First()?.BulkDeleteLogId,
                Messages = messages.Select(x => x.Message).ToArray()
            };
        }
    }
}

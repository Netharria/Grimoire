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

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.UpdateMessage
{
    public class UpdateMessageCommandHandler : ICommandHandler<UpdateMessageCommand, UpdateMessageCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public UpdateMessageCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<UpdateMessageCommandResponse> Handle(UpdateMessageCommand command, CancellationToken cancellationToken)
        {
            var message = await this._cybermancyDbContext.Messages
                .WhereLoggingIsEnabled()
                .WhereIdIs(command.MessageId)
                .Select(x => new UpdateMessageCommandResponse
                {
                    UpdateMessageLogChannelId = x.Guild.LogSettings.EditChannelLogId,
                    MessageId = x.Id,
                    UserId = x.UserId,
                    MessageContent = x.MessageHistory
                        .OrderBy(x => x.TimeStamp)
                        .Where(x => x.Action != MessageAction.Deleted)
                        .First().MessageContent,
                    Success = true
                }
                ).FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (message is null
                || message.MessageContent == command.MessageContent)
                return new UpdateMessageCommandResponse { Success = false };

            await this._cybermancyDbContext.MessageHistory.AddAsync(
                new MessageHistory
                {
                    MessageId = message.MessageId,
                    Action = MessageAction.Updated,
                    GuildId = command.GuildId,
                    MessageContent = command.MessageContent
                }, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return message;
        }
    }
}

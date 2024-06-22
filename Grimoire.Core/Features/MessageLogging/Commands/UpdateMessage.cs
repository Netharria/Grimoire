// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.MessageLogging.Commands;

public sealed class UpdateMessage
{
    public sealed record Command : ICommand<Response>
    {
        public required ulong MessageId { get; init; }
        public required ulong GuildId { get; init; }
        public string MessageContent { get; init; } = string.Empty;
    }
    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Command, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var message = await this._grimoireDbContext.Messages
            .AsNoTracking()
            .WhereMessageLoggingIsEnabled()
            .WhereIdIs(command.MessageId)
            .Select(x => new Response
            {
                UpdateMessageLogChannelId = x.Guild.MessageLogSettings.EditChannelLogId,
                MessageId = x.Id,
                UserId = x.UserId,
                MessageContent = x.MessageHistory
                    .OrderByDescending(x => x.TimeStamp)
                    .Where(x => x.Action != MessageAction.Deleted)
                    .First().MessageContent,
                Success = true,
                OriginalUserId = x.ProxiedMessageLink.OriginalMessage.UserId,
                SystemId = x.ProxiedMessageLink.SystemId,
                MemberId = x.ProxiedMessageLink.MemberId,
            }
            ).FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (message is null
                || message.MessageContent.Equals(command.MessageContent, StringComparison.CurrentCultureIgnoreCase))
                return new Response { Success = false };

            await this._grimoireDbContext.MessageHistory.AddAsync(
                new MessageHistory
                {
                    MessageId = message.MessageId,
                    Action = MessageAction.Updated,
                    GuildId = command.GuildId,
                    MessageContent = command.MessageContent
                }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return message;
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong MessageId { get; init; }
        public ulong? UpdateMessageLogChannelId { get; init; }
        public string MessageContent { get; init; } = string.Empty;
        public ulong UserId { get; init; }
        public bool Success { get; init; }
        public ulong? OriginalUserId { get; init; }
        public string? SystemId { get; init; }
        public string? MemberId { get; init; }
    }
}

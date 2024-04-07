// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Discord.Extensions;

namespace Grimoire.Core.Features.MessageLogging.Commands;

public sealed record DeleteMessage
{
    public sealed record Command : ICommand<Response>
    {
        public ulong Id { get; init; }
        public ulong GuildId { get; init; }
        public ulong? DeletedByModerator { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Command, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var message = await this._grimoireDbContext.Messages
            .AsNoTracking()
            .WhereIdIs(command.Id)
            .WhereMessageLoggingIsEnabled()
            .Where(x => x.CreatedTimestamp < DateTime.UtcNow.AddSeconds(-2))
            .Select(x => new Response
            {
                LoggingChannel = x.Guild.MessageLogSettings.DeleteChannelLogId,
                UserId = x.UserId,
                MessageContent = x.MessageHistory
                    .OrderByDescending(x => x.TimeStamp)
                    .First(y => y.Action != MessageAction.Deleted)
                    .MessageContent.UTF8toUnicode(),
                ReferencedMessage = x.ReferencedMessageId,
                Attachments = x.Attachments
                    .Select(x => new AttachmentDto
                    {
                        Id = x.Id,
                        FileName = x.FileName,
                    })
                    .ToArray(),
                Success = true,
                OriginalUserId = x.ProxiedMessageLink.OriginalMessage.UserId,
                SystemId = x.ProxiedMessageLink.SystemId,
                MemberId = x.ProxiedMessageLink.MemberId,

            }).FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (message is null)
                return new Response { Success = false };
            await this._grimoireDbContext.MessageHistory.AddAsync(new MessageHistory
            {
                MessageId = command.Id,
                Action = MessageAction.Deleted,
                GuildId = command.GuildId,
                DeletedByModeratorId = command.DeletedByModerator
            }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return message;
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong? LoggingChannel { get; init; }
        public ulong UserId { get; init; }
        public string? MessageContent { get; init; }
        public ulong? ReferencedMessage { get; init; }
        public AttachmentDto[] Attachments { get; init; } = [];
        public required bool Success { get; init; }
        public ulong? OriginalUserId { get; init; }
        public string? SystemId { get; init; }
        public string? MemberId { get; init; }
    }
}



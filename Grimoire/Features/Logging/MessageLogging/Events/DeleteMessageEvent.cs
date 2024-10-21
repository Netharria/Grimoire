// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.LogCleanup.Commands;
using Grimoire.Features.MessageLogging.Commands;
using Grimoire.PluralKit;

namespace Grimoire.Features.Logging.MessageLogging.Events;

public sealed class DeleteMessageEvent
{
    public sealed class EventHandler(IMediator mediator,
        IDiscordImageEmbedService attachmentUploadService,
        IDiscordAuditLogParserService logParserService,
        IPluralkitService pluralKitService) : IEventHandler<MessageDeletedEventArgs>
    {
        private readonly IMediator _mediator = mediator;
        private readonly IDiscordImageEmbedService _attachmentUploadService = attachmentUploadService;
        private readonly IDiscordAuditLogParserService _logParserService = logParserService;
        private readonly IPluralkitService _pluralKitService = pluralKitService;


        public async Task HandleEventAsync(DiscordClient sender, MessageDeletedEventArgs args)
        {
            if (args.Guild is null)
                return;

            var pluralkitMessage = await this._pluralKitService.GetProxiedMessageInformation(args.Message.Id, args.Message.CreationTimestamp);

            if (pluralkitMessage is not null
                && ulong.TryParse(pluralkitMessage.Id, out var proxyMessageId)
                && ulong.TryParse(pluralkitMessage.OriginalId, out var originalMessageId)
                && proxyMessageId != args.Message.Id)
            {
                await this._mediator.Send(new LinkProxyMessage.Command
                {
                    ProxyMessageId = proxyMessageId,
                    OriginalMessageId = originalMessageId,
                    GuildId = args.Guild.Id,
                    SystemId = pluralkitMessage.PluralKitSystem?.Id,
                    MemberId = pluralkitMessage.Member?.Id
                });
                return;
            }

            var auditLogEntry = await this._logParserService.ParseAuditLogForDeletedMessageAsync(args.Guild.Id, args.Channel.Id, args.Message.Id);
            var response = await this._mediator.Send(
                new Command
                {
                    MessageId = args.Message.Id,
                    DeletedByModerator = auditLogEntry?.UserResponsible?.Id,
                    GuildId = args.Guild.Id
                });

            if (!response.Success || response.LoggingChannel is null)
                return;

            var avatarUrl = await sender.GetUserAvatar(
                response.OriginalUserId is null ? response.UserId : response.OriginalUserId.Value,
                 args.Guild);

            if (avatarUrl is null)
                return;

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"Message deleted in #{args.Channel.Name}")
                .AddField("Channel", ChannelExtensions.Mention(args.Channel.Id), true)
                .AddField("Message Id", args.Message.Id.ToString(), true)
                .WithTimestamp(DateTime.UtcNow)
                .WithColor(GrimoireColor.Red)
                .WithThumbnail(avatarUrl);

            if (response.OriginalUserId is null)
                embed.AddField("Author", UserExtensions.Mention(response.UserId), true);
            else
                embed.AddField("Original Author", UserExtensions.Mention(response.OriginalUserId), true)
                .AddField("System Id", string.IsNullOrWhiteSpace(response.SystemId) ? "Private" : response.SystemId, true)
                .AddField("Member Id", string.IsNullOrWhiteSpace(response.MemberId) ? "Private" : response.MemberId, true);

            if (auditLogEntry is not null && auditLogEntry.UserResponsible is not null)
                embed.AddField("Deleted By", auditLogEntry.UserResponsible.Mention, true);

            if (response.ReferencedMessage is not null)
                embed.WithDescription($"**[Reply To](https://discordapp.com/channels/{args.Guild.Id}/{args.Channel.Id}/{response.ReferencedMessage})**");

            embed.AddMessageTextToFields("**Content**", response.MessageContent, false);

            var messageBuilder = await this._attachmentUploadService.BuildImageEmbedAsync(
                response.Attachments.Select(x => x.FileName).ToArray(),
                response.UserId,
                embed);

            var message = await sender.SendMessageToLoggingChannel(response.LoggingChannel, messageBuilder);
            if (message is not null)
                await this._mediator.Send(new AddLogMessage.Command { MessageId = message.Id, ChannelId = message.ChannelId, GuildId = args.Guild.Id });

        }

    }

    public sealed record Command : ICommand<Response>
    {
        public ulong MessageId { get; init; }
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
            .WhereIdIs(command.MessageId)
            .WhereMessageLoggingIsEnabled()
            .Select(x => new Response
            {
                LoggingChannel = x.Guild.MessageLogSettings.DeleteChannelLogId,
                UserId = x.UserId,
                MessageContent = x.MessageHistory
                    .OrderByDescending(x => x.TimeStamp)
                    .First(y => y.Action != MessageAction.Deleted)
                    .MessageContent,
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
                MessageId = command.MessageId,
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



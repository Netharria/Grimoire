// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.LogCleanup.Commands;
using Grimoire.Features.Shared.PluralKit;

namespace Grimoire.Features.Logging.MessageLogging.Events;

public sealed class DeleteMessageEvent
{
    public sealed class EventHandler(
        IMediator mediator,
        IDiscordImageEmbedService attachmentUploadService,
        IDiscordAuditLogParserService logParserService,
        IPluralkitService pluralKitService) : IEventHandler<MessageDeletedEventArgs>
    {
        private readonly IDiscordImageEmbedService _attachmentUploadService = attachmentUploadService;
        private readonly IDiscordAuditLogParserService _logParserService = logParserService;
        private readonly IMediator _mediator = mediator;
        private readonly IPluralkitService _pluralKitService = pluralKitService;


        public async Task HandleEventAsync(DiscordClient sender, MessageDeletedEventArgs args)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (args.Guild is null)
                return;

            var pluralkitMessage =
                await this._pluralKitService.GetProxiedMessageInformation(args.Message.Id,
                    args.Message.CreationTimestamp);

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

            var auditLogEntry =
                await this._logParserService.ParseAuditLogForDeletedMessageAsync(args.Guild.Id, args.Channel.Id,
                    args.Message.Id);
            var response = await this._mediator.Send(
                new Command
                {
                    MessageId = args.Message.Id,
                    DeletedByModerator = auditLogEntry?.UserResponsible?.Id,
                    GuildId = args.Guild.Id
                });

            if (!response.Success)
                return;

            var message = await sender.SendMessageToLoggingChannel(response.LoggingChannel,
                async () =>
                {
                    var embed = new DiscordEmbedBuilder()
                        .WithAuthor($"Message deleted in #{args.Channel.Name}")
                        .AddField("Channel", ChannelExtensions.Mention(args.Channel.Id), true)
                        .AddField("Message Id", args.Message.Id.ToString(), true)
                        .WithTimestamp(DateTime.UtcNow)
                        .WithColor(GrimoireColor.Red);
                    var avatarUrl = await sender.GetUserAvatar(
                        response.OriginalUserId ?? response.UserId,
                        args.Guild);
                    if (avatarUrl is not null)
                        embed.WithThumbnail(avatarUrl);

                    if (response.OriginalUserId is null)
                        embed.AddField("Author", UserExtensions.Mention(response.UserId), true);
                    else
                        embed.AddField("Original Author", UserExtensions.Mention(response.OriginalUserId), true)
                            .AddField("System Id",
                                string.IsNullOrWhiteSpace(response.SystemId) ? "Private" : response.SystemId,
                                true)
                            .AddField("Member Id",
                                string.IsNullOrWhiteSpace(response.MemberId) ? "Private" : response.MemberId,
                                true);

                    if (auditLogEntry is not null && auditLogEntry.UserResponsible is not null)
                        embed.AddField("Deleted By", auditLogEntry.UserResponsible.Mention, true);

                    if (response.ReferencedMessage is not null)
                        embed.WithDescription(
                            $"**[Reply To](https://discordapp.com/channels/{args.Guild.Id}/{args.Channel.Id}/{response.ReferencedMessage})**");

                    embed.AddMessageTextToFields("**Content**", response.MessageContent, false);

                    return await this._attachmentUploadService.BuildImageEmbedAsync(
                        response.Attachments.Select(x => x.FileName).ToArray(),
                        response.UserId,
                        embed);
                });

            if (message is not null)
                await this._mediator.Send(new AddLogMessage.Command
                {
                    MessageId = message.Id, ChannelId = message.ChannelId, GuildId = args.Guild.Id
                });
        }
    }

    public sealed record Command : IRequest<Response>
    {
        public ulong MessageId { get; init; }
        public ulong GuildId { get; init; }
        public ulong? DeletedByModerator { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var message = await dbContext.Messages
                .AsNoTracking()
                .WhereIdIs(command.MessageId)
                .WhereMessageLoggingIsEnabled()
                .Select(message => new Response
                {
                    LoggingChannel = message.Guild.MessageLogSettings.DeleteChannelLogId,
                    UserId = message.UserId,
                    MessageContent = message.MessageHistory
                        .OrderByDescending(messageHistory => messageHistory.TimeStamp)
                        .First(messageHistory => messageHistory.Action != MessageAction.Deleted)
                        .MessageContent,
                    ReferencedMessage = message.ReferencedMessageId,
                    Attachments = message.Attachments
                        .Select(attachment => new AttachmentDto { Id = attachment.Id, FileName = attachment.FileName })
                        .ToArray(),
                    Success = true,
                    OriginalUserId = message.ProxiedMessageLink.OriginalMessage.UserId,
                    SystemId = message.ProxiedMessageLink.SystemId,
                    MemberId = message.ProxiedMessageLink.MemberId
                }).FirstOrDefaultAsync(cancellationToken);
            if (message is null)
                return new Response { Success = false };
            await dbContext.MessageHistory.AddAsync(
                new MessageHistory
                {
                    MessageId = command.MessageId,
                    Action = MessageAction.Deleted,
                    GuildId = command.GuildId,
                    DeletedByModeratorId = command.DeletedByModerator
                }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
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

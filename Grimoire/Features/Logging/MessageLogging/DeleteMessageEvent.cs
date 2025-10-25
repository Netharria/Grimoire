// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using EntityFramework.Exceptions.Common;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.PluralKit;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Logging.MessageLogging;

public sealed partial class DeleteMessageEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    IDiscordImageEmbedService attachmentUploadService,
    IDiscordAuditLogParserService logParserService,
    IPluralkitService pluralKitService,
    SettingsModule settingsModule,
    GuildLog guildLog,
    ILogger<DeleteMessageEvent> logger) : IEventHandler<MessageDeletedEventArgs>
{
    private readonly IDiscordImageEmbedService _attachmentUploadService = attachmentUploadService;
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly ILogger<DeleteMessageEvent> _logger = logger;
    private readonly IDiscordAuditLogParserService _logParserService = logParserService;
    private readonly IPluralkitService _pluralKitService = pluralKitService;
    private readonly SettingsModule _settingsModule = settingsModule;


    public async Task HandleEventAsync(DiscordClient sender, MessageDeletedEventArgs args)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (args.Guild is null
            || args.Message.Author?.Id == args.Guild.CurrentMember.Id
            || await this._settingsModule.IsModuleEnabled(Module.MessageLog, args.Guild.Id))
            return;

        var pluralkitMessage =
            await this._pluralKitService.GetProxiedMessageInformation(args.Message.Id,
                args.Message.CreationTimestamp);

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        if (pluralkitMessage is not null
            && ulong.TryParse(pluralkitMessage.Id, out var proxyMessageId)
            && ulong.TryParse(pluralkitMessage.OriginalId, out var originalMessageId)
            && proxyMessageId != args.Message.Id)
        {
            await dbContext.AddAsync(
                new ProxiedMessageLink
                {
                    ProxyMessageId = proxyMessageId,
                    OriginalMessageId = originalMessageId,
                    SystemId = pluralkitMessage.PluralKitSystem?.Id,
                    MemberId = pluralkitMessage.Member?.Id
                });
            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (UniqueConstraintException)
            {
                LogProxiedMessageFailure(this._logger);
            }
            catch (ReferenceConstraintException)
            {
                LogProxiedMessageFailure(this._logger);
            }
            catch (Exception ex)
            {
                LogProxiedMessageFailure(this._logger, ex.Message, ex);
                throw;
            }

            return;
        }

        var auditLogEntry =
            await this._logParserService.ParseAuditLogForDeletedMessageAsync(args.Guild.Id, args.Channel.Id,
                args.Message.Id);
        var message = await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.Id == args.Message.Id)
            .Select(message => new Response
            {
                UserId = message.UserId,
                MessageContent = message.MessageHistory
                    .OrderByDescending(messageHistory => messageHistory.TimeStamp)
                    .First(messageHistory => messageHistory.Action != MessageAction.Deleted)
                    .MessageContent,
                ReferencedMessage = message.ReferencedMessageId,
                Attachments = message.Attachments
                    .Select(attachment => new AttachmentDto { Id = attachment.Id, FileName = attachment.FileName })
                    .ToArray(),
                OriginalUserId = message.ProxiedMessageLink!.OriginalMessage!.UserId,
                SystemId = message.ProxiedMessageLink.SystemId,
                MemberId = message.ProxiedMessageLink.MemberId
            }).FirstOrDefaultAsync();
        if (message is null)
            return;
        await dbContext.MessageHistory.AddAsync(
            new MessageHistory
            {
                MessageId = args.Message.Id,
                Action = MessageAction.Deleted,
                MessageContent = message.MessageContent ?? string.Empty,
                GuildId = args.Guild.Id,
                DeletedByModeratorId = auditLogEntry?.UserResponsible?.Id
            });
        await dbContext.SaveChangesAsync();


        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomMessage
        {
            GuildId = args.Guild.Id,
            GuildLogType = GuildLogType.MessageDeleted,
            Message = await BuildLogMessage(sender, args, message, auditLogEntry)
        });
    }

    private async Task<DiscordMessageBuilder> BuildLogMessage(
        DiscordClient sender,
        MessageDeletedEventArgs args,
        Response response,
        DiscordAuditLogMessageEntry? auditLogEntry)
    {
        var embed = new DiscordEmbedBuilder()
            .WithAuthor($"Message deleted in #{args.Channel.Name}")
            .AddField("Channel", args.Channel.Mention, true)
            .AddField("Message Id", args.Message.Id.ToString(), true)
            .WithTimestamp(DateTime.UtcNow)
            .WithColor(GrimoireColor.Red);
        var avatarUrl = await sender.GetUserAvatar(
            response.OriginalUserId ?? response.UserId,
            args.Guild);
        if (avatarUrl is not null)
            embed.WithThumbnail(avatarUrl);

        if (response.OriginalUserId is null && args.Message.Author is not null)
            embed.AddField("Author", args.Message.Author.Mention, true);
        else if (response.OriginalUserId is not null)
        {
            var user = await sender.GetUserOrDefaultAsync(response.OriginalUserId.Value);
            if (user is not null)
                embed.AddField("Original Author", user.Mention, true);
            embed.AddField("System Id",
                    string.IsNullOrWhiteSpace(response.SystemId) ? "Private" : response.SystemId,
                    true)
                .AddField("Member Id",
                    string.IsNullOrWhiteSpace(response.MemberId) ? "Private" : response.MemberId,
                    true);
        }

        if (auditLogEntry?.UserResponsible is not null)
            embed.AddField("Deleted By", auditLogEntry.UserResponsible.Mention, true);

        if (response.ReferencedMessage is not null)
            embed.WithDescription(
                $"**[Reply To](https://discordapp.com/channels/{args.Guild.Id}/{args.Channel.Id}/{response.ReferencedMessage})**");

        embed.AddMessageTextToFields("**Content**", response.MessageContent, false);

        return await this._attachmentUploadService.BuildImageEmbedAsync(
            response.Attachments.Select(x => x.FileName).ToArray(),
            response.UserId,
            embed);
    }

    [LoggerMessage(LogLevel.Error, "Was not able to save Proxied Message for the following reason. {message}")]
    static partial void LogProxiedMessageFailure(ILogger logger, string message, Exception ex);

    [LoggerMessage(LogLevel.Error, "Was not able to save Proxied Message due to violating a unique constraint.")]
    static partial void LogProxiedMessageFailure(ILogger logger);

    public sealed record Response
    {
        public ulong UserId { get; init; }
        public string? MessageContent { get; init; }
        public ulong? ReferencedMessage { get; init; }
        public AttachmentDto[] Attachments { get; init; } = [];
        public ulong? OriginalUserId { get; init; }
        public string? SystemId { get; init; }
        public string? MemberId { get; init; }
    }
}

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
    private readonly ILogger<DeleteMessageEvent> _logger = logger;

    public async Task HandleEventAsync(DiscordClient sender, MessageDeletedEventArgs args)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (args.Guild is null
            || args.Message.Author?.Id == args.Guild.CurrentMember.Id
            || !await settingsModule.IsModuleEnabled(Module.MessageLog, args.Guild.GetGuildId())
                .OrDefaultDuringMigration(false))
            return;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        // Everything below needs the stored row, a proxy link included because of its foreign key, so look it up
        // before calling out. A message that was never logged, or was purged, costs nothing more.
        var message = await dbContext.Messages
            .AsNoTracking()
            .Where(m => m.Id == args.Message.GetMessageId())
            .Select(m => new Response
            {
                UserId = m.UserId,
                Content = m.MessageHistory
                    .OfType<MessageHistoryContentEntry>()
                    .OrderByDescending(h => h.Timestamp)
                    .Select(h => (MessageContent?)h.Content)
                    .FirstOrDefault(),
                ReferencedMessage = m.ReferencedMessageId,
                Attachments = m.Attachments
                    .Select(a => new AttachmentDto { Id = a.Id, AttachmentUrl = a.AttachmentUrl })
                    .ToArray(),
                Proxy = m.ProxiedMessageLink == null
                    ? null
                    : new ProxyAuthor(m.ProxiedMessageLink.OriginalMessage!.UserId, m.ProxiedMessageLink.SystemId,
                        m.ProxiedMessageLink.MemberId)
            })
            .FirstOrDefaultAsync();

        // The author check above needs the cached message. Rows saved before the bot stopped saving its own posts
        // are too old to be cached, so check the stored author too.
        if (message is null || message.UserId.Value == args.Guild.CurrentMember.Id)
            return;

        // PluralKit deletes each original it proxies. That isn't a member's delete, so it's linked or skipped, not
        // logged (spec 1.5, section 2.2). A failed lookup comes back as NotProxied: the delete is logged as usual.
        var lookup = await pluralKitService.Lookup(args.Message.GetMessageId(), args.Message.CreationTimestamp);
        if (lookup is ProxyLookup.ReplacedByProxyOfDeletedMember)
            return;

        if (lookup is ProxyLookup.ReplacedByProxy(var link))
        {
            await dbContext.AddAsync(link);
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

        var auditLogEntry = await logParserService.ParseAuditLogForDeletedMessageAsync(args.Guild.GetGuildId(),
            args.Channel.GetChannelId(), message.UserId);

        await dbContext.MessageHistory.AddAsync(
            auditLogEntry?.UserResponsible?.Id is { } modId
                ? new MessageDeletedByModeratorEntry
                {
                    MessageId = args.Message.GetMessageId(),
                    ModeratorId = new ModeratorId(modId),
                    Timestamp = DateTimeOffset.UtcNow
                }
                : new MessageDeletedEntry
                {
                    MessageId = args.Message.GetMessageId(),
                    Timestamp = DateTimeOffset.UtcNow
                });
        await dbContext.SaveChangesAsync();

        // The history is kept either way, but building the log means an avatar lookup and downloading every image,
        // so it's built only when someone will see it (LOG-2).
        await guildLog.SendLogMessageAsync(args.Guild.GetGuildId(), GuildLogType.MessageDeleted,
            async () => new GuildLogMessageCustomMessage
            {
                GuildId = args.Guild.GetGuildId(),
                GuildLogType = GuildLogType.MessageDeleted,
                Message = await this.BuildLogMessage(sender, args, message, auditLogEntry)
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
            response.Proxy?.OriginalUserId ?? response.UserId,
            args.Guild);
        if (avatarUrl is not null)
            embed.WithThumbnail(avatarUrl);

        embed.AddMessageAuthorFields(response.UserId, response.Proxy);

        if (auditLogEntry?.UserResponsible is not null)
            embed.AddField("Deleted By", auditLogEntry.UserResponsible.Mention, true);

        if (response.ReferencedMessage is not null)
            embed.WithDescription(
                $"**[Reply To](https://discordapp.com/channels/{args.Guild.Id}/{args.Channel.Id}/{response.ReferencedMessage})**");

        embed.AddMessageTextToFields("**Content**", response.Content?.ToString() ?? string.Empty, false);

        return await attachmentUploadService.BuildImageEmbedAsync(
            response.Attachments.Select(x => x.AttachmentUrl.Value).ToArray(),
            response.UserId,
            embed);
    }

    [LoggerMessage(LogLevel.Error, "Was not able to save Proxied Message for the following reason. {message}")]
    static partial void LogProxiedMessageFailure(ILogger logger, string message, Exception ex);

    [LoggerMessage(LogLevel.Error, "Was not able to save Proxied Message due to violating a unique constraint.")]
    static partial void LogProxiedMessageFailure(ILogger logger);

    public sealed record Response
    {
        public UserId UserId { get; init; }
        public MessageContent? Content { get; init; }
        public MessageId? ReferencedMessage { get; init; }
        public AttachmentDto[] Attachments { get; init; } = [];
        public ProxyAuthor? Proxy { get; init; }
    }
}

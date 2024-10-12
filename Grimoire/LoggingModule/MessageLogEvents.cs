// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Grimoire.Features.LogCleanup.Commands;
using Grimoire.Features.MessageLogging.Commands;
using Grimoire.PluralKit;
using Microsoft.Extensions.Logging;

namespace Grimoire.LoggingModule;

public sealed partial class MessageLogEvents(IMediator mediator, IDiscordImageEmbedService attachmentUploadService, IDiscordAuditLogParserService logParserService, IPluralkitService pluralKitService, ILogger<MessageLogEvents> logger)
{
    private readonly IMediator _mediator = mediator;
    private readonly IDiscordImageEmbedService _attachmentUploadService = attachmentUploadService;
    private readonly IDiscordAuditLogParserService _logParserService = logParserService;
    private readonly IPluralkitService _pluralKitService = pluralKitService;
    private readonly ILogger<MessageLogEvents> _logger = logger;

    public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreatedEventArgs args)
    {
        if (args.Guild is null
            || args.Message.MessageType is not DiscordMessageType.Default and not DiscordMessageType.Reply)
            return;

        List<ulong> parentChannelTree = [ args.Channel.Id ];
        var channelParent = args.Channel.Parent;
        while (channelParent is not null)
        {
            parentChannelTree.Add(channelParent.Id);
            channelParent = channelParent.Parent;
        }

        await this._mediator.Send(new AddMessage.Command
        {
            Attachments = args.Message.Attachments
                .Select(x =>
                    new AttachmentDto
                    {
                        Id = x.Id,
                        FileName = string.IsNullOrEmpty(x.Url) ? "" : x.Url,
                    }).ToArray(),
            UserId = args.Author.Id,
            ChannelId = args.Channel.Id,
            MessageContent = args.Message.Content,
            MessageId = args.Message.Id,
            ReferencedMessageId = args.Message?.ReferencedMessage?.Id,
            GuildId = args.Guild.Id,
            ParentChannelTree = parentChannelTree
        });
    }

    public async Task DiscordOnMessageDeleted(DiscordClient sender, MessageDeletedEventArgs args)
    {
        if (args.Guild is null)
            return;

        var pluralkitMessage = await _pluralKitService.GetProxiedMessageInformation(args.Message.Id, args.Message.CreationTimestamp);

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
            new DeleteMessage.Command
            {
                Id = args.Message.Id,
                DeletedByModerator = auditLogEntry?.UserResponsible?.Id,
                GuildId = args.Guild.Id
            });
        if (!response.Success || response.LoggingChannel is not ulong loggingChannelId)
            return;
        var channel = await sender.GetChannelOrDefaultAsync(loggingChannelId);
        if (channel is not DiscordChannel loggingChannel)
            return;
        string avatarUrl;
        if (args.Guild.Members.TryGetValue(response.OriginalUserId is null ? response.UserId : response.OriginalUserId.Value, out var member))
        {
            if (member.IsBot) return;
            avatarUrl = member.GetGuildAvatarUrl(ImageFormat.Auto);
        }
        else
        {
            var user = await sender.GetUserAsync(response.OriginalUserId is null ? response.UserId : response.OriginalUserId.Value);
            if (user is null) return;
            avatarUrl = user.GetAvatarUrl(ImageFormat.Auto);
        }
        var embed = new DiscordEmbedBuilder()
            .WithAuthor($"Message deleted in #{args.Channel.Name}");

        if (response.OriginalUserId is null)
            embed.AddField("Author", UserExtensions.Mention(response.UserId), true);

        embed.AddField("Channel", ChannelExtensions.Mention(args.Channel.Id), true)
            .AddField("Message Id", args.Message.Id.ToString(), true)
            .WithTimestamp(DateTime.UtcNow)
            .WithColor(GrimoireColor.Red)
            .WithThumbnail(avatarUrl);
        if (auditLogEntry is not null && auditLogEntry.UserResponsible is not null)
            embed.AddField("Deleted By", auditLogEntry.UserResponsible.Mention, true);
        if (response.ReferencedMessage is not null)
            embed.WithDescription($"**[Reply To](https://discordapp.com/channels/{args.Guild.Id}/{args.Channel.Id}/{response.ReferencedMessage})**");
        if (response.OriginalUserId is not null)
            embed.AddField("Original Author", UserExtensions.Mention(response.OriginalUserId), true)
            .AddField("System Id", string.IsNullOrWhiteSpace(response.SystemId) ? "Private" : response.SystemId, true)
            .AddField("Member Id", string.IsNullOrWhiteSpace(response.MemberId) ? "Private" : response.MemberId, true)
            .WithThumbnail(avatarUrl);
        embed
            .AddMessageTextToFields("**Content**", response.MessageContent, false);

        var messageBuilder = await this._attachmentUploadService.BuildImageEmbedAsync(
                response.Attachments.Select(x => x.FileName).ToArray(),
                response.UserId,
                embed);
        try
        {
            var message = await loggingChannel.SendMessageAsync(messageBuilder);
            if (message is null) return;
            await this._mediator.Send(new AddLogMessage.Command { MessageId = message.Id, ChannelId = loggingChannel.Id, GuildId = args.Guild.Id });
        }
        catch (Exception ex)
        {
            LogUnableToSendDeleteMessage(_logger, ex, loggingChannel);
            throw;
        }
    }

    [LoggerMessage(LogLevel.Warning, "Was not able to send delete message log to {Channel}")]
    private static partial void LogUnableToSendDeleteMessage(ILogger<MessageLogEvents> logger, Exception ex, DiscordChannel channel);

    public async Task DiscordOnMessagesBulkDeleted(DiscordClient sender, MessagesBulkDeletedEventArgs args)
    {
        if (args.Guild is null)
            return;
        var response = await this._mediator.Send(
            new BulkDeleteMessageCommand
            {
                Ids = args.Messages.Select(x => x.Id).ToArray(),
                GuildId = args.Guild.Id
            });
        if (!response.Success || !response.Messages.Any() || response.BulkDeleteLogChannelId is not ulong loggingChannelId)
            return;
        var channel = await sender.GetChannelOrDefaultAsync(loggingChannelId);
        if (channel is not DiscordChannel loggingChannel) return;

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Bulk Message Delete")
            .WithDescription($"**Message Count:** {response.Messages.Count()}\n" +
                            $"**Channel:** {ChannelExtensions.Mention(response.Messages.First().ChannelId)}\n" +
                            "Full message dump attached.")
            .WithColor(GrimoireColor.Red);
        var stringBuilder = new StringBuilder();
        foreach (var message in response.Messages)
        {
            var author = args.Guild.Members[message.UserId];
            stringBuilder.AppendFormat(
                "Author: {0} ({1})\n" +
                "Id: {2}\n" +
                "Content: {3}\n" +
                (message.Attachments.Length != 0 ? "Attachments: {4}\n" : string.Empty),
                author.GetUsernameWithDiscriminator(),
                message.UserId,
                message.MessageId,
                message.MessageContent,
                string.Join("\n", message.Attachments.Select(x => x.FileName)))
                .AppendLine();
        }
        using var memoryStream = new MemoryStream();
        var writer = new StreamWriter(memoryStream);
        await writer.WriteAsync(stringBuilder);
        await writer.FlushAsync();
        memoryStream.Position = 0;

        try
        {
            var message = await loggingChannel.SendMessageAsync(new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddFile($"{DateTime.UtcNow:r}.txt", memoryStream));
            if (message is null) return;
            await this._mediator.Send(new AddLogMessage.Command { MessageId = message.Id, ChannelId = loggingChannel.Id, GuildId = args.Guild.Id });
        }
        catch (Exception ex)
        {
            LogUnableToSendBulkDeleteMessage(_logger, ex, loggingChannel);
            throw;
        }
    }

    [LoggerMessage(LogLevel.Warning, "Was not able to send bulk delete message log to {Channel}")]
    private static partial void LogUnableToSendBulkDeleteMessage(ILogger<MessageLogEvents> logger, Exception ex, DiscordChannel channel);

    public async Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdatedEventArgs args)
    {
        if (args.Guild is null)
            return;
        if (string.IsNullOrWhiteSpace(args.Message.Content)) return;
        var response = await this._mediator.Send(
            new UpdateMessage.Command
            {
                MessageId = args.Message.Id,
                GuildId = args.Guild.Id,
                MessageContent = args.Message.Content
            });
        if (!response.Success
            || response.UpdateMessageLogChannelId is not ulong loggingChannelId)
            return;
        var channel = await sender.GetChannelOrDefaultAsync(loggingChannelId);
        if (channel is not DiscordChannel loggingChannel)
            return;

        string avatarUrl;
        if (args.Guild.Members.TryGetValue(response.UserId, out var member))
        {
            if (member.IsBot) return;
            avatarUrl = member.GetGuildAvatarUrl(ImageFormat.Auto);
        }
        else
        {
            var user = await sender.GetUserAsync(response.UserId);
            if (user is null) return;
            avatarUrl = user.GetAvatarUrl(ImageFormat.Auto);
        }
        var embeds = new List<DiscordEmbedBuilder>();
        var embed = new DiscordEmbedBuilder()
            .WithDescription($"**[Jump Url]({args.Message.JumpLink})**")
            .AddField("Author", UserExtensions.Mention(response.UserId), true)
            .AddField("Channel", args.Channel.Mention, true)
            .AddField("Message Id", response.MessageId.ToString(), true)
            .WithAuthor($"Message edited in #{args.Channel.Name}")
            .WithTimestamp(DateTime.UtcNow)
            .WithColor(GrimoireColor.Yellow)
            .WithThumbnail(avatarUrl);

        if (response.OriginalUserId is not null)
            embed.AddField("Original Author", UserExtensions.Mention(response.OriginalUserId), true)
            .AddField("System Id", string.IsNullOrWhiteSpace(response.SystemId) ? "Private" : response.SystemId, true)
            .AddField("Member Id", string.IsNullOrWhiteSpace(response.MemberId) ? "Private" : response.MemberId, true);
        embed.AddMessageTextToFields("Before", response.MessageContent);
        embeds.Add(embed);
        if (response.MessageContent.Length + args.Message.Content.Length >= 5000)
        {
            embeds.Add(new DiscordEmbedBuilder()
            .AddField("Author", UserExtensions.Mention(response.UserId), true)
            .AddField("Channel", args.Channel.Mention, true)
            .AddField("Message Id", response.MessageId.ToString(), true)
            .AddField("Link", $"**[Jump Url]({args.Message.JumpLink})**", true)
            .WithAuthor($"Message edited in #{args.Channel.Name}")
            .WithTimestamp(DateTime.UtcNow)
            .WithColor(GrimoireColor.Yellow)
            .WithThumbnail(avatarUrl)
            .AddMessageTextToFields("After", args.Message.Content));
        }
        else
        {
            embeds.First().AddMessageTextToFields("After", args.Message.Content);
        }
        try
        {
            foreach (var embedToSend in embeds)
            {
                var message = await loggingChannel.SendMessageAsync(new DiscordMessageBuilder()
                    .AddEmbed(embedToSend));
                if (message is null) return;
                await this._mediator.Send(new AddLogMessage.Command { MessageId = message.Id, ChannelId = loggingChannel.Id, GuildId = args.Guild.Id });
            }
        }
        catch (BadRequestException ex)
        {
            LogUnableToSendEditMessage(_logger, ex, loggingChannel, ex.Errors ?? "None Listed");
            throw;
        }
    }

    [LoggerMessage(LogLevel.Warning, "Was not able to send edit message log to {Channel}. Errors: {errors}")]
    private static partial void LogUnableToSendEditMessage(ILogger<MessageLogEvents> logger, BadRequestException ex, DiscordChannel channel, string errors);
}

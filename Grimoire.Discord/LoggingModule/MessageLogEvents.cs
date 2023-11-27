// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Core.Features.LogCleanup.Commands;
using Grimoire.Core.Features.MessageLogging.Commands;
using Microsoft.Extensions.Logging;

namespace Grimoire.Discord.LoggingModule;

[DiscordMessageCreatedEventSubscriber]
[DiscordMessageDeletedEventSubscriber]
[DiscordMessagesBulkDeletedEventSubscriber]
[DiscordMessageUpdatedEventSubscriber]
public class MessageLogEvents(IMediator mediator, IDiscordImageEmbedService attachmentUploadService, IDiscordAuditLogParserService logParserService) :
    IDiscordMessageCreatedEventSubscriber,
    IDiscordMessageDeletedEventSubscriber,
    IDiscordMessagesBulkDeletedEventSubscriber,
    IDiscordMessageUpdatedEventSubscriber
{
    private readonly IMediator _mediator = mediator;
    private readonly IDiscordImageEmbedService _attachmentUploadService = attachmentUploadService;
    private readonly IDiscordAuditLogParserService _logParserService = logParserService;

    public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
    {
        if (args.Guild is null
            || args.Message.MessageType is not MessageType.Default and not MessageType.Reply)
            return;
        await this._mediator.Send(new AddMessageCommand
        {
            Attachments = args.Message.Attachments
                .Select(x =>
                    new AttachmentDto
                    {
                        Id = x.Id,
                        FileName = x.FileName,
                    }).ToArray(),
            UserId = args.Author.Id,
            ChannelId = args.Channel.Id,
            MessageContent = args.Message.Content,
            MessageId = args.Message.Id,
            ReferencedMessageId = args.Message?.ReferencedMessage?.Id,
            GuildId = args.Guild.Id
        });
    }

    public async Task DiscordOnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs args)
    {
        var auditLogEntry = await this._logParserService.ParseAuditLogForDeletedMessageAsync(args.Guild.Id, args.Channel.Id, args.Message.Id);
        var response = await this._mediator.Send(
            new DeleteMessageCommand
            {
                Id = args.Message.Id,
                DeletedByModerator = auditLogEntry?.UserResponsible.Id,
                GuildId = args.Guild.Id
            });
        if (!response.Success || response.LoggingChannel is not ulong loggingChannelId)
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
            if (user is null || user.IsBot) return;
            avatarUrl = user.GetAvatarUrl(ImageFormat.Auto);
        }

        var embed = new DiscordEmbedBuilder()
            .WithAuthor($"Message deleted in #{args.Channel.Name}")
            .AddField("Author", UserExtensions.Mention(response.UserId), true)
            .AddField("Channel", ChannelExtensions.Mention(args.Channel.Id), true)
            .AddField("Message Id", args.Message.Id.ToString(), true)
            .WithTimestamp(DateTime.UtcNow)
            .WithColor(GrimoireColor.Red)
            .WithThumbnail(avatarUrl);
        if (auditLogEntry is not null)
            embed.AddField("Deleted By", auditLogEntry.UserResponsible.Mention, true);
        if (response.ReferencedMessage is not null)
            embed.WithDescription($"**[Reply To](https://discordapp.com/channels/{args.Guild.Id}/{args.Channel.Id}/{response.ReferencedMessage})**");
        embed
            .AddMessageTextToFields("**Content**", response.MessageContent, false);

        var messageBuilder = await this._attachmentUploadService.BuildImageEmbedAsync(
                response.Attachments,
                response.UserId,
                args.Channel.Id,
                embed);
        try
        {
            var message = await loggingChannel.SendMessageAsync(messageBuilder);
            if (message is null) return;
            await this._mediator.Send(new AddLogMessageCommand { MessageId = message.Id, ChannelId = loggingChannel.Id, GuildId = args.Guild.Id });
        }
        catch (Exception ex)
        {
            sender.Logger.Log(LogLevel.Warning, "Was not able to send delete message log to {ChannelName} : {Exception}", loggingChannel, ex);
            throw;
        }
    }

    public async Task DiscordOnMessagesBulkDeleted(DiscordClient sender, MessageBulkDeleteEventArgs args)
    {
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
                message.Attachments)
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
            await this._mediator.Send(new AddLogMessageCommand { MessageId = message.Id, ChannelId = loggingChannel.Id, GuildId = args.Guild.Id });
        }
        catch (Exception ex)
        {
            sender.Logger.Log(LogLevel.Warning, "Was not able to send bulk delete message log to {ChannelName} : {Exception}", loggingChannel, ex);
            throw;
        }
    }

    public async Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdateEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Message.Content)) return;
        var response = await this._mediator.Send(
            new UpdateMessageCommand
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
            if (user is null || user.IsBot) return;
            avatarUrl = user.GetAvatarUrl(ImageFormat.Auto);
        }
        var embeds = new List<DiscordEmbedBuilder>
        {
            new DiscordEmbedBuilder()
            .WithDescription($"**[Jump Url]({args.Message.JumpLink})**")
            .AddField("Author", UserExtensions.Mention(response.UserId), true)
            .AddField("Channel", args.Channel.Mention, true)
            .AddField("Message Id", response.MessageId.ToString(), true)
            .WithAuthor($"Message edited in #{args.Channel.Name}")
            .WithTimestamp(DateTime.UtcNow)
            .WithColor(GrimoireColor.Yellow)
            .WithThumbnail(avatarUrl)
            .AddMessageTextToFields("Before", response.MessageContent)
        };

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
            foreach (var embed in embeds)
            {
                var message = await loggingChannel.SendMessageAsync(new DiscordMessageBuilder()
                    .AddEmbed(embed));
                if (message is null) return;
                await this._mediator.Send(new AddLogMessageCommand { MessageId = message.Id, ChannelId = loggingChannel.Id, GuildId = args.Guild.Id });
            }
        }
        catch (Exception ex)
        {
            sender.Logger.Log(LogLevel.Warning, "Was not able to send edit message log to {ChannelName} : {Exception}", loggingChannel, ex);
            throw;
        }
    }
}

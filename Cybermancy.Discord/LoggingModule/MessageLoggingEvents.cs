// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Core.Extensions;
using Cybermancy.Core.Features.Logging.Commands.AddLogMessage;
using Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.AddMessage;
using Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.BulkDeleteMessages;
using Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteMessage;
using Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.UpdateMessage;
using Cybermancy.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using MediatR;
using Microsoft.Extensions.Logging;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.LoggingModule
{
    [DiscordMessageCreatedEventSubscriber]
    [DiscordMessageDeletedEventSubscriber]
    [DiscordMessagesBulkDeletedEventSubscriber]
    [DiscordMessageUpdatedEventSubscriber]
    public class MessageLoggingEvents :
        IDiscordMessageCreatedEventSubscriber,
        IDiscordMessageDeletedEventSubscriber,
        IDiscordMessagesBulkDeletedEventSubscriber,
        IDiscordMessageUpdatedEventSubscriber
    {
        private readonly IMediator _mediator;

        public MessageLoggingEvents(IMediator mediator)
        {
            this._mediator = mediator;
        }



        public Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
            => this._mediator.Send(new AddMessageCommand
            {
                Attachments = args.Message.Attachments.Select(x => x.Url).ToArray(),
                AuthorId = args.Author.Id,
                ChannelId = args.Channel.Id,
                CreatedTimestamp = args.Message.CreationTimestamp.UtcDateTime,
                MessageContent = args.Message.Content,
                MessageId = args.Message.Id,
                ReferencedMessageId = args.Message?.ReferencedMessage?.Id,
                GuildId = args.Guild.Id
            });

        public async Task DiscordOnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs args)
        {
            var auditLogEntry = await args.Guild.GetRecentAuditLogAsync(AuditLogActionType.MessageDelete);
            var response = await this._mediator.Send(
                new DeleteMessageCommand
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
            
            string userName;
            string avatarUrl;
            if (args.Guild.Members.TryGetValue(response.AuthorId, out var member))
            {
                if (member.IsBot && !member.Roles.Any(x => x.Id == 732962687360827472)) return;
                userName = member.GetUsernameWithDiscriminator();
                avatarUrl = member.GetGuildAvatarUrl(ImageFormat.Auto);
            }
            else
            {
                var user = await sender.GetUserAsync(response.AuthorId);
                if (user is null || user.IsBot) return;
                userName = user.GetUsernameWithDiscriminator();
                avatarUrl = user.GetAvatarUrl(ImageFormat.Auto);
            }

            var embeds = new List<DiscordEmbed>();

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{userName} ({response.AuthorId})")
                .WithTitle($"Message deleted in #{args.Channel.Name}")
                .WithDescription($"**Author:** {UserExtensions.Mention(response.AuthorId)}\n" +
                                $"**Channel:** {ChannelExtensions.Mention(args.Channel.Id)}\n" +
                                $"**Message Id:** {args.Message.Id}" +
                                (auditLogEntry is null
                                ? string.Empty
                                : $"\n**Deleted By:** {auditLogEntry.UserResponsible.Mention}")
                                )
                .WithTimestamp(DateTime.UtcNow)
                .WithThumbnail(avatarUrl)
                .AddMessageTextToFields("**Content**", response.MessageContent, false);

            if (response.AttachmentUrls.Any())
            {
                embed.AddField("Attachments",
                    string.Join(' ', response.AttachmentUrls))
                    .WithImageUrl(response.AttachmentUrls.FirstOrDefault());

                if (response.AttachmentUrls.Length > 1)
                    for (var i = 1; i < response.AttachmentUrls.Length; i++)
                        embeds.Add(new DiscordEmbedBuilder()
                            .WithDescription($"**Message Id:** {args.Message.Id}")
                            .WithAuthor($"{UserExtensions.Mention(response.AuthorId)} ({response.AuthorId})")
                            .Build());
            }
            try
            {
                var message = await loggingChannel.SendMessageAsync(new DiscordMessageBuilder()
                .AddEmbeds(embeds.Prepend(embed)));
                if (message is null) return;
                await _mediator.Send(new AddLogMessageCommand { MessageId = message.Id, ChannelId = loggingChannel.Id, GuildId = args.Guild.Id });
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
            if (!response.Success || response.BulkDeleteLogChannelId is not ulong loggingChannelId)
                return;
            var channel = await sender.GetChannelOrDefaultAsync(loggingChannelId);
            if (channel is not DiscordChannel loggingChannel) return;
            if (!response.Messages.Any() || !response.Success) return;

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Bulk Message Delete")
                .WithDescription($"**Message Count:** {response.Messages.Count()}\n" +
                                $"**Channel:** {ChannelExtensions.Mention(response.Messages.First().ChannelId)}\n" +
                                "Full message dump attached.");
            var stringBuilder = new StringBuilder();
            foreach (var message in response.Messages)
            {
                var author = args.Guild.Members[message.AuthorId];
                stringBuilder.AppendFormat(
                    "Author: {0} ({1})\n" +
                    "Id: {2}\n" +
                    "Content: {3}\n" +
                    (message.AttachmentUrls.Any() ? "Attachments: {4}\n": string.Empty),
                    author.GetUsernameWithDiscriminator(),
                    message.AuthorId,
                    message.MessageId,
                    message.MessageContent,
                    message.AttachmentUrls)
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
                .WithFile($"{DateTime.UtcNow:r}.txt", memoryStream));
                if (message is null) return;
                await _mediator.Send(new AddLogMessageCommand { MessageId = message.Id, ChannelId = loggingChannel.Id, GuildId = args.Guild.Id });
            }
            catch (Exception ex)
            {
                sender.Logger.Log(LogLevel.Warning, "Was not able to send bulk delete message log to {ChannelName} : {Exception}", loggingChannel, ex);
                throw;
            }
        }

        public async Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdateEventArgs args)
        {
            if (args.Message.Content.Length == 0) return;
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
            
            string userName;
            string avatarUrl;
            if (args.Guild.Members.TryGetValue(response.AuthorId, out var member))
            {
                if (member.IsBot && !member.Roles.Any(x => x.Id == 732962687360827472)) return;
                userName = member.GetUsernameWithDiscriminator();
                avatarUrl = member.GetGuildAvatarUrl(ImageFormat.Auto);
            }
            else
            {
                var user = await sender.GetUserAsync(response.AuthorId);
                if (user is null || user.IsBot) return;
                userName = user.GetUsernameWithDiscriminator();
                avatarUrl = user.GetAvatarUrl(ImageFormat.Auto);
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"Message edited in #{args.Channel.Name}")
                .WithDescription($"**Author:** {UserExtensions.Mention(response.AuthorId)}\n" +
                                $"**Channel:** {args.Channel.Mention}\n" +
                                $"**Message Id:** {response.MessageId}\n" +
                                $"**[Jump Url]({args.Message.JumpLink})**")
                .WithAuthor($"{userName} ({response.AuthorId})")
                .WithTimestamp(DateTime.UtcNow)
                .WithThumbnail(avatarUrl)
                .AddMessageTextToFields("Before", response.MessageContent)
                .AddMessageTextToFields("After", args.Message.Content);

            try
            {
                var message = await loggingChannel.SendMessageAsync(new DiscordMessageBuilder()
                .AddEmbed(embed));
                if (message is null) return;
                await _mediator.Send(new AddLogMessageCommand { MessageId = message.Id, ChannelId = loggingChannel.Id, GuildId = args.Guild.Id });
            }
            catch (Exception ex)
            {
                sender.Logger.Log(LogLevel.Warning, "Was not able to send edit message log to {ChannelName} : {Exception}", loggingChannel, ex);
                throw;
            }
        }
    }
}

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Core.Enums;
using Cybermancy.Core.Extensions;
using Cybermancy.Core.Features.Logging.Commands.AddLogMessage;
using Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.AddMessage;
using Cybermancy.Core.Features.Logging.Queries.GetLoggingChannels;
using Cybermancy.Core.Features.Logging.Queries.MessageLogQueries.GetMessage;
using Cybermancy.Core.Features.Logging.Queries.MessageLogQueries.GetMessages;
using Cybermancy.Core.Features.Shared.Queries.GetModuleStateForGuild;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
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



        public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
        {
            if (!await this._mediator.Send(new GetModuleStateForGuildQuery { GuildId = args.Guild.Id, Module = Module.Logging }))
                return;
            await this._mediator.Send(new AddMessageCommand
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
        }

        public async Task DiscordOnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs args)
        {
            if (!await this._mediator.Send(new GetModuleStateForGuildQuery { GuildId = args.Guild.Id, Module = Module.Logging }))
                return;
            var loggingChannels = await this._mediator.Send(new GetLoggingChannelsQuery { GuildId = args.Guild.Id });
            if (loggingChannels.DeleteChannelLogId is not ulong deleteChannelId)
                return;
            DiscordChannel loggingChannel;

            try
            {
                loggingChannel = await sender.GetChannelAsync(deleteChannelId);
            }
            catch (Exception ex) when (ex is BadRequestException || ex is ServerErrorException)
            {
                return;
            }

            var response = await this._mediator.Send(new GetMessageQuery{ MessageId = args.Message.Id });
            var author = args.Guild.Members[response.AuthorId];
            var embeds = new List<DiscordEmbed>();

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{author.DisplayName} ({response.AuthorId})")
                .WithTitle($"Message deleted in #{ChannelExtensions.Mention(response.ChannelId)}")
                .WithDescription($"**Author:** {UserExtensions.Mention(response.AuthorId)}\n" +
                                $"**Channel:** {ChannelExtensions.Mention(response.ChannelId)}\n" +
                                $"**Message Id:** {response.MessageId}")
                .WithTimestamp(DateTime.UtcNow)
                .WithThumbnail(
                !string.IsNullOrWhiteSpace(author.GuildAvatarUrl)
                ? author.GuildAvatarUrl
                : !string.IsNullOrWhiteSpace(author.AvatarUrl)
                ? author.AvatarUrl
                : author.DefaultAvatarUrl);

            if(!string.IsNullOrWhiteSpace(response.MessageContent))
                embed.AddField("Content", response.MessageContent);

            if (response.AttachmentUrls.Any())
            {
                embed.AddField("Attachments",
                    string.Join(' ', response.AttachmentUrls))
                    .WithImageUrl(response.AttachmentUrls.FirstOrDefault());

                if (response.AttachmentUrls.Count > 1)
                    for (var i = 1; i < response.AttachmentUrls.Count; i++)
                        embeds.Add(new DiscordEmbedBuilder()
                            .WithDescription($"**Message Id:** {response.MessageId}")
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
            if (!await this._mediator.Send(new GetModuleStateForGuildQuery { GuildId = args.Guild.Id, Module = Module.Logging }))
                return;
            var loggingChannels = await this._mediator.Send(new GetLoggingChannelsQuery { GuildId = args.Guild.Id });
            if (loggingChannels.BulkDeleteChannelLogId is not ulong bulkDeleteChannelLogId)
                return;
            DiscordChannel loggingChannel;

            try
            {
                loggingChannel = await sender.GetChannelAsync(bulkDeleteChannelLogId);
            }
            catch (Exception ex) when (ex is BadRequestException || ex is ServerErrorException)
            {
                return;
            }

            var response = await this._mediator.Send(new GetMessagesQuery{ MesssageIds = args.Messages.Select(x => x.Id ).ToArray() });

            if (!response.Messages.Any() || !response.Success) return;

            var embed = new DiscordEmbedBuilder()
                .WithTitle("**Bulk Message Delete**")
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
                    "Content: {3}\n",
                    author.DisplayName,
                    message.AuthorId,
                    message.MessageId,
                    message.MessageContent)
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
            if (!await this._mediator.Send(new GetModuleStateForGuildQuery { GuildId = args.Guild.Id, Module = Module.Logging }))
                return;
            var loggingChannels = await this._mediator.Send(new GetLoggingChannelsQuery { GuildId = args.Guild.Id });
            if (loggingChannels.BulkDeleteChannelLogId is not ulong bulkDeleteChannelLogId)
                return;
            DiscordChannel loggingChannel;

            try
            {
                loggingChannel = await sender.GetChannelAsync(bulkDeleteChannelLogId);
            }
            catch (Exception ex) when (ex is BadRequestException || ex is ServerErrorException)
            {
                return;
            }


            //try
            //{
            //    var message = await loggingChannel.SendMessageAsync(new DiscordMessageBuilder()
            //    .AddEmbed(embed));
            //    if (message is null) return;
            //    await _mediator.Send(new AddLogMessageCommand { MessageId = message.Id, ChannelId = loggingChannel.Id, GuildId = args.Guild.Id });
            //}
            //catch (Exception ex)
            //{
            //    sender.Logger.Log(LogLevel.Warning, "Was not able to send edit message log to {ChannelName} : {Exception}", loggingChannel, ex);
            //    throw;
            //}
        }
    }
}

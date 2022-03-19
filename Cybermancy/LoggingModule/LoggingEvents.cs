// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Enums;
using Cybermancy.Core.Extensions;
using Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.AddMessage;
using Cybermancy.Core.Features.Logging.Queries.GetLoggingChannels;
using Cybermancy.Core.Features.Logging.Queries.MessageLogQueries.GetMessage;
using Cybermancy.Core.Features.Shared.Queries.GetModuleStateForGuild;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.LoggingModule
{
    [DiscordMessageEventsSubscriber]
    public class LoggingEvents : IDiscordMessageEventsSubscriber
    {
        private readonly IMediator _mediator;

        public LoggingEvents(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public Task DiscordOnMessageAcknowledged(DiscordClient sender, MessageAcknowledgeEventArgs args) => Task.CompletedTask;
        public Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
            => _mediator.Send(new AddMessageCommand
            {
                Attachments = args.Message.Attachments.Select(x => x.Url).ToArray(),
                AuthorId = args.Author.Id,
                ChannelId = args.Channel.Id,
                CreatedTimestamp = args.Message.CreationTimestamp.UtcDateTime,
                MessageContent = args.Message.Content,
                MessageId = args.Message.Id,
                ReferencedMessageId = args.Message.ReferencedMessage.Id
            });
        public async Task DiscordOnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs args)
        {
            if (!await _mediator.Send(new GetModuleStateForGuildQuery { GuildId = args.Guild.Id, Module = Module.Logging }))
                return;
            var loggingChannels = await _mediator.Send(new GetLoggingChannelsQuery { GuildId = args.Guild.Id });
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
            catch (NotFoundException)
            {
                //Delete channel from settings.
            }

            var response = await _mediator.Send(new GetMessageQuery{ MessageId = args.Message.Id });

            var embeds = new List<DiscordEmbed>();

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{response.AuthorName} ({response.AuthorId})")
                .WithTitle($"Message deleted in #{response.ChannelName}")
                .WithDescription($"**Author:** {UserExtensions.Mention(response.AuthorId)}\n" +
                                $"**Channel:** {ChannelExtensions.Mention(response.ChannelId)}\n" +
                                $"**Message Id:** {response.MessageId}")
                .WithTimestamp(DateTime.UtcNow)
                .WithThumbnail(response.AuthorAvatarUrl);

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
                            .WithAuthor($"{response.AuthorName} ({response.AuthorId})")
                            .Build());
            }

            var message = await args.Channel.SendMessageAsync(new DiscordMessageBuilder()
                .AddEmbeds(embeds.Prepend(embed)));
            
            //await _mediator.Send(new AddLogMessage{ MessageId = message.id })
        }
        public Task DiscordOnMessagesBulkDeleted(DiscordClient sender, MessageBulkDeleteEventArgs args) => Task.CompletedTask;
        public Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdateEventArgs args) => Task.CompletedTask;
    }
}

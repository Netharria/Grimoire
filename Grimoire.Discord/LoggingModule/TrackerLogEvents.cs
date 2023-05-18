// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging.Queries.GetTracker;
using Grimoire.Core.Features.Logging.Queries.GetTrackerWithOldMessage;

namespace Grimoire.Discord.LoggingModule
{
    [DiscordMessageCreatedEventSubscriber]
    [DiscordMessageUpdatedEventSubscriber]
    [DiscordVoiceStateUpdatedEventSubscriber]
    public class TrackerLogEvents :
        IDiscordMessageCreatedEventSubscriber,
        IDiscordMessageUpdatedEventSubscriber,
        IDiscordVoiceStateUpdatedEventSubscriber
    {
        private readonly IMediator _mediator;
        private readonly IDiscordImageEmbedService _imageEmbedService;

        public TrackerLogEvents(IMediator mediator, IDiscordImageEmbedService imageEmbedService)
        {
            this._mediator = mediator;
            this._imageEmbedService = imageEmbedService;
        }


        public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
        {
            if (args.Guild is null) return;
            var response = await this._mediator.Send(new GetTrackerQuery{ UserId = args.Author.Id, GuildId = args.Guild.Id });
            if (response is null) return;

            var loggingChannel = args.Guild.Channels.GetValueOrDefault(response.TrackerChannelId);
            if (loggingChannel is null) return;

            var embeds = new List<DiscordEmbed>();

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{args.Message.Channel.Name}")
                .WithDescription($"**[Jump URL]({args.Message.JumpLink})**")
                .WithFooter($"{args.Author.GetUsernameWithDiscriminator()}\t{args.Author.Id}\nMessage Sent",
                    args.Author.GetAvatarUrl(ImageFormat.Auto))
                .WithTimestamp(DateTime.UtcNow)
                .AddMessageTextToFields("**Content**", args.Message.Content, false);

            var messageBuilder = await this._imageEmbedService.BuildImageEmbedAsync(
                    args.Message.Attachments.Select(x => x.Url).ToArray(),
                    args.Author.Id,
                    embed);
            await loggingChannel.SendMessageAsync(messageBuilder);
        }
        public async Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdateEventArgs args)
        {
            if (args.Message.Content.Length == 0) return;
            var response = await this._mediator.Send(
                new GetTrackerWithOldMessageQuery
                {
                    UserId = args.Author.Id,
                    GuildId = args.Guild.Id,
                    MessageId = args.Message.Id
                });
            if (response is null) return;
            var loggingChannel = args.Guild.Channels.GetValueOrDefault(response.TrackerChannelId);
            if (loggingChannel is null) return;

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{args.Message.Channel.Mention}")
                .WithDescription($"**[Jump URL]({args.Message.JumpLink})**")
                .WithTimestamp(DateTime.UtcNow)
                .WithFooter($"{args.Author.GetUsernameWithDiscriminator()}\t{args.Author.Id}\nMessage Edited",
                    args.Author.GetAvatarUrl(ImageFormat.Auto))
                .AddMessageTextToFields("Before", response.OldMessageContent)
                .AddMessageTextToFields("After", args.Message.Content);

            await loggingChannel.SendMessageAsync(embed);
        }
        public async Task DiscordOnVoiceStateUpdated(DiscordClient sender, VoiceStateUpdateEventArgs args)
        {
            var response = await this._mediator.Send(new GetTrackerQuery{ UserId = args.User.Id, GuildId = args.Guild.Id });
            if (response is null) return;

            var loggingChannel = args.Guild.Channels.GetValueOrDefault(response.TrackerChannelId);
            if (loggingChannel is null) return;

            if (args.Before.Channel == args.After.Channel) return;
            DiscordEmbedBuilder embed;
            if (args.Before.Channel is null)
            {
                embed = new DiscordEmbedBuilder()
                    .WithAuthor("Joined Voice Channel")
                    .WithDescription($"**Channel:** {args.After.Channel.Name}")
                    .WithFooter($"{args.User.GetUsernameWithDiscriminator()}\n{args.User.Id}")
                    .WithTimestamp(DateTime.UtcNow);
                await loggingChannel.SendMessageAsync(embed);
                return;
            }

            if (args.After.Channel is null)
            {
                embed = new DiscordEmbedBuilder()
                    .WithAuthor("Left Voice Channel")
                    .WithDescription($"**Channel:** {args.Before.Channel.Name}")
                    .WithFooter($"{args.User.GetUsernameWithDiscriminator()}\n{args.User.Id}")
                    .WithTimestamp(DateTime.UtcNow);
                await loggingChannel.SendMessageAsync(embed);
                return;
            }

            embed = new DiscordEmbedBuilder()
                    .WithAuthor("Moved Voice Channels")
                    .WithDescription($"**From:** {args.Before.Channel.Name}\n" +
                    $"**To:** {args.After.Channel.Name}")
                    .WithFooter($"{args.User.GetUsernameWithDiscriminator()}\n{args.User.Id}")
                    .WithTimestamp(DateTime.UtcNow);
            await loggingChannel.SendMessageAsync(embed);
        }
    }
}

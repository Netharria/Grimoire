// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging.Queries.GetTracker;
using Grimoire.Core.Features.Logging.Queries.GetTrackerWithOldMessage;

namespace Grimoire.Discord.LoggingModule;

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
            .AddField("User", args.Author.Mention, true)
            .AddField("Channel", args.Channel.Mention, true)
            .AddField("Link", $"**[Jump URL]({args.Message.JumpLink})**", true)
            .WithFooter("Message Sent", args.Author.GetAvatarUrl(ImageFormat.Auto))
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
            .AddField("User", args.Author.Mention, true)
            .AddField("Channel", args.Channel.Mention, true)
            .AddField("Link", $"**[Jump URL]({args.Message.JumpLink})**", true)
            .WithFooter("Message Sent", args.Author.GetAvatarUrl(ImageFormat.Auto))
            .WithTimestamp(DateTime.UtcNow)
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

        if(args.Before?.Channel is null && args.After?.Channel is null) return;

        if (args.Before?.Channel is null)
        {
            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor("Joined Voice Channel")
                .AddField("User", args.User.Mention, true)
                .AddField("Channel", args.After.Channel.Mention, true)
                .WithTimestamp(DateTime.UtcNow));
            return;
        }

        if (args.After?.Channel is null)
        {
            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor("Left Voice Channel")
                .AddField("User", args.User.Mention, true)
                .AddField("Channel", args.Before.Channel.Mention, true)
                .WithTimestamp(DateTime.UtcNow));
            return;
        }

        if(args.Before.Channel != args.After.Channel)
        {
            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithAuthor("Moved Voice Channels")
                    .AddField("User", args.User.Mention, true)
                    .AddField("From", args.Before.Channel.Mention, true)
                    .AddField("To", args.After.Channel.Mention, true)
                    .WithTimestamp(DateTime.UtcNow));
        }
    }
}

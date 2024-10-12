// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.MessageLogging.Queries;

namespace Grimoire.LoggingModule;

internal sealed class TrackerLogEvents(IMediator mediator, IDiscordImageEmbedService imageEmbedService)
{
    private readonly IMediator _mediator = mediator;
    private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;

    public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreatedEventArgs args)
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
                args.Message.Attachments.Select(x => x.Url).OfType<string>().ToArray(),
                args.Author.Id,
                embed);
        await loggingChannel.SendMessageAsync(messageBuilder);
    }
    public async Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdatedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Message.Content)) return;
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
    public async Task DiscordOnVoiceStateUpdated(DiscordClient sender, VoiceStateUpdatedEventArgs args)
    {
        var response = await this._mediator.Send(new GetTrackerQuery{ UserId = args.User.Id, GuildId = args.Guild.Id });
        if (response is null) return;

        var loggingChannel = args.Guild.Channels.GetValueOrDefault(response.TrackerChannelId);

        if (loggingChannel is null) return;

        if (args.Before?.Channel is null && args.After?.Channel is null) return;

        if (args.Before?.Channel is null)
        {
            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor("Joined Voice Channel")
                .AddField("User", args.User.Mention, true)
                .AddField("Channel", args.After.Channel is null ? "" : args.After.Channel.Mention, true)
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

        if (args.Before.Channel != args.After.Channel)
            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithAuthor("Moved Voice Channels")
                    .AddField("User", args.User.Mention, true)
                    .AddField("From", args.Before.Channel.Mention, true)
                    .AddField("To", args.After.Channel.Mention, true)
                    .WithTimestamp(DateTime.UtcNow));
    }
}

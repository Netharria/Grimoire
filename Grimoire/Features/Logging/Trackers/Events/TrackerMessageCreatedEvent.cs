// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.TrackerLog;

namespace Grimoire.Features.Logging.Trackers.Events;

internal sealed class TrackerMessageCreatedEvent(
    IMediator mediator,
    IDiscordImageEmbedService imageEmbedService,
    TrackerLog trackerLog)
    : IEventHandler<MessageCreatedEventArgs>
{
    private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;
    private readonly IMediator _mediator = mediator;
    private readonly TrackerLog _trackerLog = trackerLog;

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (args.Guild is null) return;
        var response =
            await this._mediator.Send(new GetTracker.Query { UserId = args.Author.Id, GuildId = args.Guild.Id });
        if (response is null) return;

        await this._trackerLog.SendTrackerMessageAsync(new TrackerMessageCustomMessage
        {
            GuildId = args.Guild.Id,
            TrackerId = response.TrackerChannelId,
            TrackerIdType = TrackerIdType.ChannelId,
            Message = await this._imageEmbedService.BuildImageEmbedAsync(
                args.Message.Attachments.Select(x => x.Url).OfType<string>().ToArray(),
                args.Author.Id,
                new DiscordEmbedBuilder()
                    .AddField("User", args.Author.Mention, true)
                    .AddField("Channel", args.Channel.Mention, true)
                    .AddField("Link", $"**[Jump URL]({args.Message.JumpLink})**", true)
                    .WithFooter("Message Sent", args.Author.GetAvatarUrl(MediaFormat.Auto))
                    .WithTimestamp(DateTime.UtcNow)
                    .AddMessageTextToFields("**Content**", args.Message.Content, false))
        });
    }
}

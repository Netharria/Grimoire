// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Logging.Trackers.Events;

internal sealed class TrackerMessageCreatedEvent(IMediator mediator, IDiscordImageEmbedService imageEmbedService)
    : IEventHandler<MessageCreatedEventArgs>
{
    private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;
    private readonly IMediator _mediator = mediator;

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (args.Guild is null) return;
        var response =
            await this._mediator.Send(new GetTracker.Query { UserId = args.Author.Id, GuildId = args.Guild.Id });
        if (response is null) return;

        await sender.SendMessageToLoggingChannel(response.TrackerChannelId,
            async () =>
            {
                var embed = new DiscordEmbedBuilder()
                    .AddField("User", args.Author.Mention, true)
                    .AddField("Channel", args.Channel.Mention, true)
                    .AddField("Link", $"**[Jump URL]({args.Message.JumpLink})**", true)
                    .WithFooter("Message Sent", args.Author.GetAvatarUrl(ImageFormat.Auto))
                    .WithTimestamp(DateTime.UtcNow)
                    .AddMessageTextToFields("**Content**", args.Message.Content, false);

                return await this._imageEmbedService.BuildImageEmbedAsync(
                    args.Message.Attachments.Select(x => x.Url).OfType<string>().ToArray(),
                    args.Author.Id,
                    embed);
            });
    }
}

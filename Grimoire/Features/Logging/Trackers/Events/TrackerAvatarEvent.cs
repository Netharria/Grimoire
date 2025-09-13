// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.TrackerLog;
using Grimoire.Notifications;

namespace Grimoire.Features.Logging.Trackers.Events;

internal sealed class TrackerAvatarEvent(
    IMediator mediator,
    IDiscordImageEmbedService imageEmbedService,
    TrackerLog trackerLog) : INotificationHandler<AvatarUpdatedNotification>
{
    private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;
    private readonly TrackerLog _trackerLog = trackerLog;
    private readonly IMediator _mediator = mediator;

    public async Task Handle(AvatarUpdatedNotification notification, CancellationToken cancellationToken)
    {
        var response = await this._mediator.Send(
            new GetTracker.Query { UserId = notification.UserId, GuildId = notification.GuildId }, cancellationToken);
        if (response is null)
            return;

        await this._trackerLog.SendTrackerMessageAsync(new TrackerMessageCustomMessage
        {
            GuildId = notification.GuildId,
            TrackerId = response.TrackerChannelId,
            TrackerIdType = TrackerIdType.ChannelId,
            Message = await this._imageEmbedService.BuildImageEmbedAsync(
                [notification.AfterAvatar],
                notification.UserId,
                new DiscordEmbedBuilder()
                    .WithAuthor("Avatar Updated")
                    .AddField("User", UserExtensions.Mention(notification.UserId))
                    .WithThumbnail(notification.AfterAvatar)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .WithColor(GrimoireColor.Purple),
                false)
        }, cancellationToken);
    }
}

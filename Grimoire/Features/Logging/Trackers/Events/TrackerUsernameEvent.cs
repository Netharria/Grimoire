// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.TrackerLog;
using Grimoire.Notifications;

namespace Grimoire.Features.Logging.Trackers.Events;

internal sealed class TrackerUsernameEvent(IMediator mediator, TrackerLog trackerLog)
    : INotificationHandler<UsernameTrackerNotification>
{
    private readonly IMediator _mediator = mediator;
    private readonly TrackerLog _trackerLog = trackerLog;

    public async Task Handle(UsernameTrackerNotification notification, CancellationToken cancellationToken)
    {
        var response = await this._mediator.Send(
            new GetTracker.Query { UserId = notification.UserId, GuildId = notification.GuildId }, cancellationToken);

        if (response is null) return;

        await this._trackerLog.SendTrackerMessageAsync(new TrackerMessageCustomEmbed
        {
            GuildId = notification.GuildId,
            TrackerId = notification.UserId,
            TrackerIdType = TrackerIdType.UserId,
            Embed = new DiscordEmbedBuilder()
                .WithAuthor("Username Updated")
                .AddField("User", UserExtensions.Mention(notification.UserId))
                .AddField("Before",
                    string.IsNullOrWhiteSpace(notification.BeforeUsername) ? "`Unknown`" : notification.BeforeUsername,
                    true)
                .AddField("After",
                    string.IsNullOrWhiteSpace(notification.AfterUsername) ? "`Unknown`" : notification.AfterUsername,
                    true)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithColor(GrimoireColor.Mint)
        }, cancellationToken);
    }
}

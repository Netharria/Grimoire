// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.TrackerLog;
using Grimoire.Notifications;

namespace Grimoire.Features.Logging.Trackers.Events;

internal sealed class TrackerNicknameEvent(IMediator mediator, TrackerLog trackerLog)
    : INotificationHandler<NicknameUpdatedNotification>
{
    private readonly IMediator _mediator = mediator;
    private readonly TrackerLog _trackerLog = trackerLog;

    public async Task Handle(NicknameUpdatedNotification notification, CancellationToken cancellationToken)
    {
        var response = await this._mediator.Send(
            new GetTracker.Query { UserId = notification.UserId, GuildId = notification.GuildId }
            , cancellationToken);

        if (response is null)
            return;
        await this._trackerLog.SendTrackerMessageAsync(new TrackerMessageCustomEmbed
        {
            GuildId = notification.GuildId,
            TrackerId = response.TrackerChannelId,
            TrackerIdType = TrackerIdType.ChannelId,
            Embed = new DiscordEmbedBuilder()
                .WithAuthor("Nickname Updated")
                .AddField("User", UserExtensions.Mention(notification.UserId))
                .AddField("Before",
                    string.IsNullOrWhiteSpace(notification.BeforeNickname) ? "None" : notification.BeforeNickname, true)
                .AddField("After",
                    string.IsNullOrWhiteSpace(notification.AfterNickname) ? "None" : notification.AfterNickname, true)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithColor(GrimoireColor.Mint)
        }, cancellationToken);
    }
}

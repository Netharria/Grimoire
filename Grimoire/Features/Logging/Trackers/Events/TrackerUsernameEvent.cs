// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Notifications;

namespace Grimoire.Features.Logging.Trackers.Events;

internal sealed class TrackerUsernameEvent(DiscordClient discordClient, IMediator mediator) : INotificationHandler<UsernameTrackerNotification>
{
    private readonly DiscordClient _discordClient = discordClient;
    private readonly IMediator _mediator = mediator;

    public async Task Handle(UsernameTrackerNotification notification, CancellationToken cancellationToken)
    {
        var response = await this._mediator.Send(new GetTracker.Query{ UserId = notification.UserId, GuildId = notification.GuildId }, cancellationToken);

        if (response is null) return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Username Updated")
            .AddField("User", UserExtensions.Mention(notification.UserId))
            .AddField("Before", string.IsNullOrWhiteSpace(notification.BeforeUsername)? "`Unknown`" : notification.BeforeUsername, true)
            .AddField("After", string.IsNullOrWhiteSpace(notification.AfterUsername)? "`Unknown`" : notification.AfterUsername, true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Mint);
        await this._discordClient.SendMessageToLoggingChannel(response.TrackerChannelId, embed);
    }
}

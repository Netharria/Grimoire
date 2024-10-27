// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Notifications;

namespace Grimoire.Features.Logging.Trackers.Events;

internal sealed class TrackerNicknameEvent(DiscordClient discordClient, IMediator mediator)
    : INotificationHandler<NicknameUpdatedNotification>
{
    private readonly DiscordClient _discordClient = discordClient;
    private readonly IMediator _mediator = mediator;

    public async Task Handle(NicknameUpdatedNotification notification, CancellationToken cancellationToken)
    {
        var response = await this._mediator.Send(
            new GetTracker.Query { UserId = notification.UserId, GuildId = notification.GuildId }
            , cancellationToken);

        if (response is null)
            return;

        await this._discordClient.SendMessageToLoggingChannel(response.TrackerChannelId,
            embed => embed
                .WithAuthor("Nickname Updated")
                .AddField("User", UserExtensions.Mention(notification.UserId))
                .AddField("Before",
                    string.IsNullOrWhiteSpace(notification.BeforeNickname) ? "None" : notification.BeforeNickname, true)
                .AddField("After",
                    string.IsNullOrWhiteSpace(notification.AfterNickname) ? "None" : notification.AfterNickname, true)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithColor(GrimoireColor.Mint));
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Notifications;

namespace Grimoire.Features.Logging.Trackers.Events;

internal sealed class TrackerAvatarEvent(DiscordClient clientService, IMediator mediator, IDiscordImageEmbedService imageEmbedService) : INotificationHandler<AvatarUpdatedNotification>
{
    private readonly DiscordClient _discordClient = clientService;
    private readonly IMediator _mediator = mediator;
    private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;

    public async ValueTask Handle(AvatarUpdatedNotification notification, CancellationToken cancellationToken)
    {
        var response = await this._mediator.Send(new GetTracker.Query{ UserId = notification.UserId, GuildId = notification.GuildId }, cancellationToken);
        if (response is null)
            return;

        var embed = new DiscordEmbedBuilder()
                .WithAuthor("Avatar Updated")
                .WithDescription($"**User:** {UserExtensions.Mention(notification.UserId)}")
                .WithColor(GrimoireColor.Purple)
                .WithTimestamp(DateTimeOffset.UtcNow);

        await this._discordClient.SendMessageToLoggingChannel(response.TrackerChannelId,
            await this._imageEmbedService.BuildImageEmbedAsync(
                [notification.AfterAvatar],
                notification.UserId,
                embed,
                false));
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging.Queries.GetTracker;
using Grimoire.Discord.Notifications;

namespace Grimoire.Discord.LoggingModule;

public class TrackerAvatarEvent(IDiscordClientService clientService, IMediator mediator, IDiscordImageEmbedService imageEmbedService) : INotificationHandler<AvatarTrackerNotification>
{
    private readonly IDiscordClientService _clientService = clientService;
    private readonly IMediator _mediator = mediator;
    private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;

    public async ValueTask Handle(AvatarTrackerNotification notification, CancellationToken cancellationToken)
    {
        var response = await this._mediator.Send(new GetTrackerQuery{ UserId = notification.UserId, GuildId = notification.GuildId }, cancellationToken);
        if (response is null) return;
        if (!this._clientService.Client.Guilds.TryGetValue(notification.GuildId, out var guild)) return;
        if (!guild.Channels.TryGetValue(response.TrackerChannelId, out var logChannel)) return;

        var embed = new DiscordEmbedBuilder()
                .WithAuthor("Avatar Updated")
                .WithDescription($"**User:** <@!{notification.UserId}>")
                .WithColor(GrimoireColor.Purple)
                .WithTimestamp(DateTimeOffset.UtcNow);


        await logChannel.SendMessageAsync(await this._imageEmbedService
            .BuildImageEmbedAsync(
            [notification.AfterAvatar],
            notification.UserId,
            embed,
            false));
    }
}

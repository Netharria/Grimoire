// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Discord.Notifications;
using Grimoire.Features.MessageLogging.Queries;

namespace Grimoire.LoggingModule;

internal sealed class TrackerAvatarEvent(DiscordClient clientService, IMediator mediator, IDiscordImageEmbedService imageEmbedService) : INotificationHandler<AvatarTrackerNotification>
{
    private readonly DiscordClient _clientService = clientService;
    private readonly IMediator _mediator = mediator;
    private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;

    public async ValueTask Handle(AvatarTrackerNotification notification, CancellationToken cancellationToken)
    {
        var response = await this._mediator.Send(new GetTrackerQuery{ UserId = notification.UserId, GuildId = notification.GuildId }, cancellationToken);
        if (response is null) return;
        if (!this._clientService.Guilds.TryGetValue(notification.GuildId, out var guild)) return;
        if (!guild.Channels.TryGetValue(response.TrackerChannelId, out var logChannel)) return;

        var embed = new DiscordEmbedBuilder()
                .WithAuthor("Avatar Updated")
                .WithDescription($"**User:** {UserExtensions.Mention(notification.UserId)}")
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

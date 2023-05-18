// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging.Queries.GetTracker;
using Grimoire.Discord.Notifications;

namespace Grimoire.Discord.LoggingModule
{
    public class TrackerUsernameEvent : INotificationHandler<UsernameTrackerNotification>
    {
        private readonly IDiscordClientService _clientService;
        private readonly IMediator _mediator;

        public TrackerUsernameEvent(IDiscordClientService clientService, IMediator mediator)
        {
            this._clientService = clientService;
            this._mediator = mediator;
        }

        public async ValueTask Handle(UsernameTrackerNotification notification, CancellationToken cancellationToken)
        {
            var response = await this._mediator.Send(new GetTrackerQuery{ UserId = notification.UserId, GuildId = notification.GuildId }, cancellationToken);

            if (response is null) return;
            if (!this._clientService.Client.Guilds.TryGetValue(notification.GuildId, out var guild)) return;
            if (!guild.Channels.TryGetValue(response.TrackerChannelId, out var logChannel)) return;

            var embed = new DiscordEmbedBuilder()
                        .WithDescription($"**Before:** {notification.BeforeUsername}\n" +
                            $"**After:** {notification.AfterUsername}")
                        .WithAuthor(notification.AfterUsername)
                        .WithFooter($"{notification.UserId}")
                        .WithTimestamp(DateTimeOffset.UtcNow);
            await logChannel.SendMessageAsync(embed);
        }
    }
}

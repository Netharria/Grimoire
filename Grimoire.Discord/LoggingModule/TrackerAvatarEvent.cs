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
    public class TrackerAvatarEvent : INotificationHandler<AvatarTrackerNotification>
    {
        private readonly IDiscordClientService _clientService;
        private readonly IMediator _mediator;
        private readonly HttpClient _httpClient;

        public TrackerAvatarEvent(IDiscordClientService clientService, IMediator mediator, IHttpClientFactory httpFactory)
        {
            this._clientService = clientService;
            this._mediator = mediator;
            this._httpClient = httpFactory.CreateClient();
        }

        public async ValueTask Handle(AvatarTrackerNotification notification, CancellationToken cancellationToken)
        {
            var response = await this._mediator.Send(new GetTrackerQuery{ UserId = notification.UserId, GuildId = notification.GuildId }, cancellationToken);
            if (response is null) return;
            if (!_clientService.Client.Guilds.TryGetValue(notification.GuildId, out var guild)) return;
            if (!guild.Channels.TryGetValue(response.TrackerChannelId, out var logChannel)) return;

            var stream = await this._httpClient.GetStreamAsync(notification.AfterAvatar, cancellationToken);
            var fileName = $"attachment{0}.{notification.AfterAvatar.Split('.')[^1].Split('?')[0]}";

            var embed = new DiscordEmbedBuilder()
                .WithDescription($"New avatar")
                .WithAuthor(notification.Username)
                .WithThumbnail(notification.BeforeAvatar)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithImageUrl($"attachment://{fileName}");

            await logChannel.SendMessageAsync(new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddFile(fileName, stream));
        }
    }
}

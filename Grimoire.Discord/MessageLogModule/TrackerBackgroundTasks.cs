// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging.Commands.TrackerCommands.RemoveExpiredTrackers;

namespace Grimoire.Discord.LoggingModule
{
    public class TrackerBackgroundTasks : INotificationHandler<TimedNotification>
    {
        private readonly IMediator _mediator;
        private readonly IDiscordClientService _discordClientService;

        public TrackerBackgroundTasks(IMediator mediator, IDiscordClientService discordClientService)
        {
            this._mediator = mediator;
            this._discordClientService = discordClientService;
        }

        public async ValueTask Handle(TimedNotification notification, CancellationToken cancellationToken)
        {
            if (notification.Time.Second % 5 != 0)
                return;
            var response = await this._mediator.Send(new RemoveExpiredTrackersCommand(), cancellationToken);
            foreach (var expiredTracker in response)
            {
                var guild = this._discordClientService.Client.Guilds.GetValueOrDefault(expiredTracker.GuildId);
                if (guild is null) continue;

                var embed = new DiscordEmbedBuilder()
                    .WithDescription($"Tracker on {UserExtensions.Mention(expiredTracker.UserId)} has expired.");

                var channel = guild.Channels.GetValueOrDefault(expiredTracker.TrackerChannelId);
                if (channel is not null)
                    await channel.SendMessageAsync(embed);

                if (expiredTracker.LogChannelId is not null)
                {
                    var ModerationLogChannel = guild.Channels.GetValueOrDefault(expiredTracker.LogChannelId.Value);
                    if (ModerationLogChannel is not null)
                        await ModerationLogChannel.SendMessageAsync(embed);
                }
            }
        }
    }
}

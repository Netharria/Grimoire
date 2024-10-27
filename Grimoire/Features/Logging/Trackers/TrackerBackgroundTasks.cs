// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Logging.Trackers;

internal sealed class TrackerBackgroundTasks(IServiceProvider serviceProvider, ILogger<TrackerBackgroundTasks> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var discord = serviceProvider.GetRequiredService<DiscordClient>();
        var response = await mediator.Send(new RemoveExpiredTrackersCommand(), stoppingToken);
        foreach (var expiredTracker in response)
        {
            var guild = discord.Guilds.GetValueOrDefault(expiredTracker.GuildId);
            if (guild is null) continue;

            var embed = new DiscordEmbedBuilder()
                .WithDescription($"Tracker on {UserExtensions.Mention(expiredTracker.UserId)} has expired.");

            var channel = guild.Channels.GetValueOrDefault(expiredTracker.TrackerChannelId);
            if (channel is not null)
                await channel.SendMessageAsync(embed).WaitAsync(stoppingToken);

            if (expiredTracker.LogChannelId is not null)
            {
                var moderationLogChannel = guild.Channels.GetValueOrDefault(expiredTracker.LogChannelId.Value);
                if (moderationLogChannel is not null)
                    await moderationLogChannel.SendMessageAsync(embed);
            }
        }
    }
}

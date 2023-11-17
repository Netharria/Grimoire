// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging.Commands.TrackerCommands;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Grimoire.Discord.LoggingModule;

public class TrackerBackgroundTasks(IServiceProvider serviceProvider, ILogger logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{

    protected override async Task RunTask(CancellationToken stoppingToken)
    {
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var discordClientService = _serviceProvider.GetRequiredService<IDiscordClientService>();
        var response = await mediator.Send(new RemoveExpiredTrackersCommand(),stoppingToken);
        foreach (var expiredTracker in response)
        {
            var guild = discordClientService.Client.Guilds.GetValueOrDefault(expiredTracker.GuildId);
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

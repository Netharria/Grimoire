// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Moderation.Commands;
using Grimoire.Core.Features.Moderation.Queries;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Grimoire.Discord.ModerationModule;

public class MuteBackgroundTasks(IServiceProvider serviceProvider, ILogger logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{
    protected override async Task RunTask(CancellationToken stoppingToken)
    {
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var discordClientService = _serviceProvider.GetRequiredService<IDiscordClientService>();

        var response = await mediator.Send(new GetExpiredMutesQuery(), stoppingToken);
        foreach (var expiredLock in response)
        {
            var guild = discordClientService.Client.Guilds.GetValueOrDefault(expiredLock.GuildId);
            if (guild is null) continue;

            var user = guild.Members.GetValueOrDefault(expiredLock.UserId);

            if (user is null) continue;
            var role = guild.Roles.GetValueOrDefault(expiredLock.MuteRole);
            if (role is null) continue;
            try
            {
                await user.RevokeRoleAsync(role);
            }
            catch (DiscordException)
            {
                if (expiredLock.LogChannelId is not null)
                {
                    var ModerationLogChannel = guild.Channels.GetValueOrDefault(expiredLock.LogChannelId.Value);
                    if (ModerationLogChannel is not null)
                        await ModerationLogChannel.SendMessageAsync(new DiscordEmbedBuilder()
                            .WithDescription($"Tried to unmute {user.Mention} but was unable to. Please remove the mute role manually."));
                }
            }
            _ = await mediator.Send(new UnmuteUserCommand { UserId = user.Id, GuildId = guild.Id }, stoppingToken);

            var embed = new DiscordEmbedBuilder()
                .WithDescription($"Mute on {user.Mention} has expired.");

            await user.SendMessageAsync(embed);

            if (expiredLock.LogChannelId is not null)
            {
                var ModerationLogChannel = guild.Channels.GetValueOrDefault(expiredLock.LogChannelId.Value);
                if (ModerationLogChannel is not null)
                    await ModerationLogChannel.SendMessageAsync(embed);
            }
        }
    }
}


// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Mute;

internal sealed class MuteBackgroundTasks(IServiceProvider serviceProvider, ILogger<MuteBackgroundTasks> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var discordClient = serviceProvider.GetRequiredService<DiscordClient>();

        await foreach (var expiredLock in mediator.CreateStream(new GetExpiredMutesQuery(), stoppingToken))
        {
            var guild = discordClient.Guilds.GetValueOrDefault(expiredLock.GuildId);
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
                    var moderationLogChannel = guild.Channels.GetValueOrDefault(expiredLock.LogChannelId.Value);
                    if (moderationLogChannel is not null)
                        await moderationLogChannel.SendMessageAsync(new DiscordEmbedBuilder()
                            .WithDescription(
                                $"Tried to unmute {user.Mention} but was unable to. Please remove the mute role manually."));
                }
            }

            _ = await mediator.Send(new UnmuteUserCommand { UserId = user.Id, GuildId = guild.Id }, stoppingToken);

            var embed = new DiscordEmbedBuilder()
                .WithDescription($"Mute on {user.Mention} has expired.");

            await user.SendMessageAsync(embed);

            if (expiredLock.LogChannelId is not null)
            {
                var moderationLogChannel = guild.Channels.GetValueOrDefault(expiredLock.LogChannelId.Value);
                if (moderationLogChannel is not null)
                    await moderationLogChannel.SendMessageAsync(embed);
            }
        }
    }
}

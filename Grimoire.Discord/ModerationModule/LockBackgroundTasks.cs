// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Moderation.Commands;
using Grimoire.Core.Features.Moderation.Queries;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Grimoire.Discord.ModerationModule;

public class LockBackgroundTasks(IServiceProvider serviceProvider, ILogger logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{

    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var discordClientService = serviceProvider.GetRequiredService<IDiscordClientService>();

        var response = await mediator.Send(new GetExpiredLocksQuery(), stoppingToken);
        foreach (var expiredLock in response)
        {
            var guild = discordClientService.Client.Guilds.GetValueOrDefault(expiredLock.GuildId);
            if (guild is null) continue;

            var channel = guild.Channels.GetValueOrDefault(expiredLock.ChannelId);
            channel ??= guild.Threads.GetValueOrDefault(expiredLock.ChannelId);

            if (channel is null) continue;

            if (!channel.IsThread)
            {
                var permissions = channel.PermissionOverwrites.First(x => x.Id == guild.EveryoneRole.Id);
                await channel.AddOverwriteAsync(guild.EveryoneRole,
                    permissions.Allowed.RevertLockPermissions(expiredLock.PreviouslyAllowed)
                    , permissions.Denied.RevertLockPermissions(expiredLock.PreviouslyDenied));
            }

            _ = await mediator.Send(new UnlockChannelCommand { ChannelId = channel.Id, GuildId = guild.Id }, stoppingToken);

            var embed = new DiscordEmbedBuilder()
                .WithDescription($"Lock on {channel.Mention} has expired.");

            await channel.SendMessageAsync(embed);

            if (expiredLock.LogChannelId is not null)
            {
                var ModerationLogChannel = guild.Channels.GetValueOrDefault(expiredLock.LogChannelId.Value);
                if (ModerationLogChannel is not null)
                    await ModerationLogChannel.SendMessageAsync(embed);
            }
        }
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using DSharpPlus.Exceptions;
using Grimoire.Features.Moderation.Mute.Commands;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Mute;

internal sealed class MuteBackgroundTasks(IServiceProvider serviceProvider, ILogger<MuteBackgroundTasks> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var settingsModule = serviceProvider.GetRequiredService<SettingsModule>();
        var discordClient = serviceProvider.GetRequiredService<DiscordClient>();
        var guildLog = serviceProvider.GetRequiredService<GuildLog>();

        await foreach (var expiredLock in settingsModule.GetAllExpiredMutes(cancellationToken))
        {
            var guild = discordClient.Guilds.GetValueOrDefault(expiredLock.GuildId);
            if (guild is null) continue;

            var user = guild.Members.GetValueOrDefault(expiredLock.UserId);

            var muteRole = await settingsModule.GetMuteRole(guild.Id, cancellationToken);

            if (user is null) continue;
            if (muteRole is null) continue;
            var role = guild.Roles.GetValueOrDefault(muteRole.Value);
            if (role is null) continue;
            try
            {
                await user.RevokeRoleAsync(role);
            }
            catch (DiscordException)
            {
                await guildLog.SendLogMessageAsync(
                    new GuildLogMessage
                    {
                        GuildId = guild.Id,
                        GuildLogType = GuildLogType.Moderation,
                        Description =
                            $"Tried to unmute {user.Mention} but was unable to. Please remove the mute role manually."
                    }, cancellationToken);
            }

            await settingsModule.RemoveMute(expiredLock.UserId, expiredLock.GuildId, cancellationToken);

            var embed = new DiscordEmbedBuilder()
                .WithDescription($"Mute on {user.Mention} has expired.");

            await user.SendMessageAsync(embed);

            await guildLog.SendLogMessageAsync(
                new GuildLogMessage
                {
                    GuildId = guild.Id,
                    GuildLogType = GuildLogType.Moderation,
                    Description = $"Mute on {user.Mention} has expired."
                }, cancellationToken);
        }
    }
}

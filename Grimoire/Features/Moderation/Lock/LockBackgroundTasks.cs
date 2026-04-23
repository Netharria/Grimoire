// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Lock;

internal sealed class LockBackgroundTasks(IServiceProvider serviceProvider, ILogger<LockBackgroundTasks> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var settingsModule = serviceProvider.GetRequiredService<SettingsModule>();
        var discordClient = serviceProvider.GetRequiredService<DiscordClient>();
        var guildLog = serviceProvider.GetRequiredService<GuildLog>();

        await foreach (var expiredLock in settingsModule.GetAllExpiredChannelLocks(cancellationToken))
        {
            var channel = await discordClient.GetChannelOrDefaultAsync(expiredLock.ChannelId, cancellationToken);

            // DSharpPlus hasn't finished implementing nullable notations
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            var everyoneRole = channel?.Guild?.EveryoneRole;
            if (channel is null || everyoneRole is null)
                continue;

            var permissions = channel.PermissionOverwrites
                .First(x => x.Id == expiredLock.GuildId.Value);
            await channel.AddOverwriteAsync(everyoneRole,
                permissions.Allowed.RevertLockPermissions(expiredLock.PreviouslyAllowed.Permissions),
                permissions.Denied.RevertLockPermissions(expiredLock.PreviouslyDenied.Permissions));

            await settingsModule.RemoveChannelLock(
                expiredLock.ChannelId,
                expiredLock.GuildId,
                discordClient.GetGrimoireModeratorId(),
                cancellationToken);

            await SendLockExpiredLogAsync(guildLog, channel, expiredLock.GuildId, cancellationToken);
        }

        await foreach (var expiredLock in settingsModule.GetAllExpiredThreadLocks(cancellationToken))
        {
            var channel = await discordClient.GetChannelOrDefaultAsync(expiredLock.ChannelId, cancellationToken);
            if (channel is null)
                continue;

            await settingsModule.RemoveThreadLock(
                expiredLock.ChannelId,
                expiredLock.GuildId,
                discordClient.GetGrimoireModeratorId(),
                cancellationToken);

            await SendLockExpiredLogAsync(guildLog, channel, expiredLock.GuildId, cancellationToken);
        }
    }

    private static async Task SendLockExpiredLogAsync(GuildLog guildLog, DiscordChannel channel, GuildId guildId,
        CancellationToken cancellationToken)
    {
        var embed = new DiscordEmbedBuilder()
            .WithDescription($"Lock on {channel.Mention} has expired.");
        await guildLog.SendLogMessageAsync(
            new GuildLogMessageCustomEmbed { GuildId = guildId, GuildLogType = GuildLogType.Moderation, Embed = embed },
            cancellationToken);
        await channel.SendMessageAsync(embed);
    }
}

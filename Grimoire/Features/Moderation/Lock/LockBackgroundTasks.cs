// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Settings.Domain;
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
        var issueReporter = serviceProvider.GetRequiredService<GuildIssueReporter>();

        await foreach (var expiredLock in settingsModule.GetAllExpiredChannelLocks(cancellationToken))
            await this.ProcessItemAsync(
                $"the expired lock on channel {expiredLock.ChannelId} in guild {expiredLock.GuildId}",
                () => ProcessExpiredChannelLockAsync(expiredLock, settingsModule, discordClient, guildLog,
                    issueReporter, cancellationToken));

        await foreach (var expiredLock in settingsModule.GetAllExpiredThreadLocks(cancellationToken))
            await this.ProcessItemAsync(
                $"the expired lock on thread {expiredLock.ChannelId} in guild {expiredLock.GuildId}",
                () => ProcessExpiredThreadLockAsync(expiredLock, settingsModule, discordClient, guildLog,
                    cancellationToken));
    }

    private static async Task ProcessExpiredChannelLockAsync(
        ChannelLocked expiredLock,
        SettingsModule settingsModule,
        DiscordClient discordClient,
        GuildLog guildLog,
        GuildIssueReporter issueReporter,
        CancellationToken cancellationToken)
    {
        // Only ask Discord about guilds the bot is in; see MuteBackgroundTasks.
        var botIsInGuild = discordClient.Guilds.ContainsKey(expiredLock.GuildId.Value);
        var channel = botIsInGuild
            ? await discordClient.GetChannelOrDefaultAsync(expiredLock.ChannelId, cancellationToken)
            : null;

        switch (ExpiredItemTriage.For(botIsInGuild, channel is not null))
        {
            case ExpiredItemAction.KeepForLater:
                return;
            case ExpiredItemAction.Resolve:
                // The channel was deleted, so there is nothing to unlock.
                await RecordChannelUnlockAsync(expiredLock, "Channel no longer exists.", settingsModule,
                    discordClient, cancellationToken);
                return;
        }

        // DSharpPlus hasn't finished implementing nullable notations
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        var everyoneRole = channel!.Guild?.EveryoneRole;
        if (everyoneRole is null)
            return;

        try
        {
            await ChannelLockReverter.RevertAsync(channel, everyoneRole, expiredLock);
        }
        catch (UnauthorizedException)
        {
            // The lock stays recorded so it is retried; tell the guild why it is still locked.
            await issueReporter.ReportAsync(
                GuildIssueCatalog.CannotUnlockChannel(expiredLock.GuildId, expiredLock.ChannelId), cancellationToken);
            throw;
        }

        await RecordChannelUnlockAsync(expiredLock, "Channel lock expired.", settingsModule, discordClient,
            cancellationToken)
            .TapAsync(_ => SendLockExpiredLogAsync(channel, guildLog, cancellationToken));
    }

    private static async Task ProcessExpiredThreadLockAsync(
        ThreadLocked expiredLock,
        SettingsModule settingsModule,
        DiscordClient discordClient,
        GuildLog guildLog,
        CancellationToken cancellationToken)
    {
        var botIsInGuild = discordClient.Guilds.ContainsKey(expiredLock.GuildId.Value);
        var channel = botIsInGuild
            ? await discordClient.GetChannelOrDefaultAsync(expiredLock.ChannelId, cancellationToken)
            : null;

        switch (ExpiredItemTriage.For(botIsInGuild, channel is not null))
        {
            case ExpiredItemAction.KeepForLater:
                return;
            case ExpiredItemAction.Resolve:
                await RecordThreadUnlockAsync(expiredLock, "Thread no longer exists.", settingsModule, discordClient,
                    cancellationToken);
                return;
        }

        await RecordThreadUnlockAsync(expiredLock, "Thread lock expired.", settingsModule, discordClient,
                cancellationToken)
            .TapAsync(_ => SendLockExpiredLogAsync(channel!, guildLog, cancellationToken));
    }

    private static Task<Result<ChannelLocked>> RecordChannelUnlockAsync(ChannelLocked expiredLock, string reason,
        SettingsModule settingsModule, DiscordClient discordClient, CancellationToken cancellationToken)
        => ModerationReason.Create(reason)
            .Bind(moderationReason =>
                ChannelUnlocked.Create(
                    discordClient.GetGrimoireModeratorId(),
                    expiredLock.ChannelId,
                    expiredLock.GuildId,
                    DateTimeOffset.UtcNow,
                    moderationReason))
            .ToResult()
            .BindAsync(channelUnlock => settingsModule.ApplyChannelLockAction(channelUnlock, cancellationToken));

    private static Task<Result<ThreadLocked>> RecordThreadUnlockAsync(ThreadLocked expiredLock, string reason,
        SettingsModule settingsModule, DiscordClient discordClient, CancellationToken cancellationToken)
        => ModerationReason.Create(reason)
            .Bind(moderationReason =>
                ThreadUnlocked.Create(
                    discordClient.GetGrimoireModeratorId(),
                    expiredLock.ChannelId,
                    expiredLock.GuildId,
                    DateTimeOffset.UtcNow,
                    moderationReason))
            .ToResult()
            .BindAsync(threadUnlock => settingsModule.ApplyThreadLockAction(threadUnlock, cancellationToken));

    private static async Task SendLockExpiredLogAsync(
        DiscordChannel channel,
        GuildLog guildLog,
        CancellationToken cancellationToken)
    {
        var embed = new DiscordEmbedBuilder()
            .WithDescription($"Lock on {channel.Mention} has expired.");
        await guildLog.SendLogMessageAsync(
            new GuildLogMessageCustomEmbed
            {
                GuildId = channel.Guild.GetGuildId(), GuildLogType = GuildLogType.Moderation, Embed = embed
            },
            cancellationToken);
        await channel.SendMessageAsync(embed);
    }
}

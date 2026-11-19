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
using LanguageExt;
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
                    issueReporter, cancellationToken),
                cancellationToken);

        await foreach (var expiredLock in settingsModule.GetAllExpiredThreadLocks(cancellationToken))
            await this.ProcessItemAsync(
                $"the expired lock on thread {expiredLock.ChannelId} in guild {expiredLock.GuildId}",
                () => ProcessExpiredThreadLockAsync(expiredLock, settingsModule, discordClient, guildLog,
                    cancellationToken),
                cancellationToken);
    }

    private static async Task ProcessExpiredChannelLockAsync(
        ChannelLock expiredLock,
        SettingsModule settingsModule,
        DiscordClient discordClient,
        GuildLog guildLog,
        GuildIssueReporter issueReporter,
        CancellationToken cancellationToken)
    {
        // Only ask Discord about guilds the bot is in; see MuteBackgroundTasks.
        var botIsInGuild = discordClient.Guilds.ContainsKey(expiredLock.GuildId.Value);
        var channel = botIsInGuild
            ? await discordClient.GetChannelOrDefaultAsync(expiredLock.ChannelId)
            : null;

        switch (ExpiredItemTriage.For(botIsInGuild, channel is not null))
        {
            case ExpiredItemAction.KeepForLater:
                return;
            case ExpiredItemAction.Resolve:
                // The channel was deleted, so there is nothing to unlock.
                await RecordChannelUnlockAsync(expiredLock, settingsModule, cancellationToken);
                return;
        }

        // A thread locked before thread locks had their own table was copied in as a channel lock. Threads have no
        // permission overwrites to revert, and trying would fail on every tick, so just let the lock expire.
        if (channel!.IsThread)
        {
            await RecordChannelUnlockAsync(expiredLock, settingsModule, cancellationToken)
                .TapAsync(_ => SendLockExpiredLogAsync(channel, guildLog, cancellationToken));
            return;
        }

        try
        {
            await ChannelLockReverter.RevertAsync(channel, channel.Guild.EveryoneRole, expiredLock);
        }
        catch (UnauthorizedException)
        {
            // The lock stays recorded so it is retried; tell the guild why it is still locked.
            await issueReporter.ReportAsync(
                GuildIssueCatalog.CannotUnlockChannel(expiredLock.GuildId, expiredLock.ChannelId), cancellationToken);
            throw;
        }

        await RecordChannelUnlockAsync(expiredLock, settingsModule, cancellationToken)
            .TapAsync(_ => SendLockExpiredLogAsync(channel, guildLog, cancellationToken));
    }

    private static async Task ProcessExpiredThreadLockAsync(
        ThreadLock expiredLock,
        SettingsModule settingsModule,
        DiscordClient discordClient,
        GuildLog guildLog,
        CancellationToken cancellationToken)
    {
        var botIsInGuild = discordClient.Guilds.ContainsKey(expiredLock.GuildId.Value);
        var channel = botIsInGuild
            ? await discordClient.GetChannelOrDefaultAsync(expiredLock.ChannelId)
            : null;

        switch (ExpiredItemTriage.For(botIsInGuild, channel is not null))
        {
            case ExpiredItemAction.KeepForLater:
                return;
            case ExpiredItemAction.Resolve:
                await RecordThreadUnlockAsync(expiredLock, settingsModule, cancellationToken);
                return;
        }

        await RecordThreadUnlockAsync(expiredLock, settingsModule, cancellationToken)
            .TapAsync(_ => SendLockExpiredLogAsync(channel!, guildLog, cancellationToken));
    }

    /// <summary>The lock ran out, or its channel is gone: it becomes Expired (spec 2.2).</summary>
    private static Task<Fin<ChannelLock>> RecordChannelUnlockAsync(ChannelLock expiredLock,
        SettingsModule settingsModule, CancellationToken cancellationToken)
        => settingsModule.ExpireChannelLock(expiredLock, cancellationToken);

    private static Task<Fin<ThreadLock>> RecordThreadUnlockAsync(ThreadLock expiredLock,
        SettingsModule settingsModule, CancellationToken cancellationToken)
        => settingsModule.ExpireThreadLock(expiredLock, cancellationToken);

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
        // Categories and forums can't hold messages; DSharpPlus refuses with ArgumentException before sending.
        if (channel.Type is not (DiscordChannelType.Category or DiscordChannelType.GuildForum or DiscordChannelType.GuildMedia))
            await channel.SendMessageAsync(embed);
    }
}

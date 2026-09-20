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

namespace Grimoire.Features.Moderation.Mute;

internal sealed class MuteBackgroundTasks(IServiceProvider serviceProvider, ILogger<MuteBackgroundTasks> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var settingsModule = serviceProvider.GetRequiredService<SettingsModule>();
        var discordClient = serviceProvider.GetRequiredService<DiscordClient>();
        var guildLog = serviceProvider.GetRequiredService<GuildLog>();
        var issueReporter = serviceProvider.GetRequiredService<GuildIssueReporter>();

        await foreach (var expiredMute in settingsModule.GetAllExpiredMutes(cancellationToken))
            await this.ProcessItemAsync(
                $"the expired mute of user {expiredMute.UserId} in guild {expiredMute.GuildId}",
                () => ProcessExpiredMuteAsync(expiredMute, settingsModule, discordClient, guildLog, issueReporter,
                    cancellationToken));
    }

    private static async Task ProcessExpiredMuteAsync(
        MuteAdded expiredMute,
        SettingsModule settingsModule,
        DiscordClient discordClient,
        GuildLog guildLog,
        GuildIssueReporter issueReporter,
        CancellationToken cancellationToken)
    {
        // Only ask Discord about guilds the bot is in. For the rest the mute is kept, so it is handled if the bot is
        // invited back, and nothing is requested on every tick.
        var botIsInGuild = discordClient.Guilds.TryGetValue(expiredMute.GuildId.Value, out var guild);
        var user = botIsInGuild ? await FindMemberAsync(guild!, expiredMute.UserId) : null;

        switch (ExpiredItemTriage.For(botIsInGuild, user is not null))
        {
            case ExpiredItemAction.KeepForLater:
                return;
            case ExpiredItemAction.Resolve:
                // The member left, so there is no role to remove and nobody to notify.
                await RemoveMuteAsync(expiredMute, settingsModule, discordClient, cancellationToken);
                return;
        }

        // Triage guarantees both exist from here on.
        var activeGuild = guild!;
        var activeUser = user!;

        var muteRoleId = await settingsModule.GetConfiguredMuteRole(activeGuild.GetGuildId(), cancellationToken)
            .GetOrElse(() => null);
        if (muteRoleId is not { } roleId || !activeGuild.Roles.TryGetValue(roleId.Value, out var role))
        {
            // Nothing can be removed, and retrying every few seconds would not change that.
            await issueReporter.ReportAsync(GuildIssueCatalog.MuteRoleMissing(activeGuild.GetGuildId()),
                cancellationToken);
            await RemoveMuteAsync(expiredMute, settingsModule, discordClient, cancellationToken);
            return;
        }

        // A moderator may already have taken the role off by hand.
        if (activeUser.Roles.Any(memberRole => memberRole.Id == role.Id))
            try
            {
                await activeUser.RevokeRoleAsync(role, "Mute expired");
            }
            catch (UnauthorizedException)
            {
                // Keep the mute recorded so it is retried (with a growing delay) once the permissions are fixed.
                await issueReporter.ReportAsync(
                    GuildIssueCatalog.CannotUnmuteMember(activeGuild.GetGuildId(), expiredMute.UserId),
                    cancellationToken);
                throw;
            }

        await RemoveMuteAsync(expiredMute, settingsModule, discordClient, cancellationToken);

        try
        {
            await activeUser.SendMessageAsync(new DiscordEmbedBuilder().WithDescription($"Mute on {activeUser.Mention} has expired."));
        }
        catch (DiscordException)
        {
            // Members with closed DMs cannot be told; the guild log below still records the expiry.
        }

        await guildLog.SendLogMessageAsync(
            new GuildLogMessage
            {
                GuildId = activeGuild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Description = $"Mute on {activeUser.Mention} has expired."
            }, cancellationToken);
    }

    /// <summary>Only a NotFound means the member is gone. Any other failure is retried on the next tick.</summary>
    private static async Task<DiscordMember?> FindMemberAsync(DiscordGuild guild, UserId userId)
    {
        try
        {
            return await guild.GetMemberAsync(userId.Value);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private static Task RemoveMuteAsync(MuteAdded mute, SettingsModule settingsModule, DiscordClient discordClient,
        CancellationToken cancellationToken)
        => settingsModule.RemoveMute(mute.UserId, mute.GuildId, new ModeratorId(discordClient.CurrentUser.Id),
            cancellationToken);
}

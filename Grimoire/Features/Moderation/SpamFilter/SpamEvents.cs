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
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.SpamFilter;

internal sealed partial class SpamEvents(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    SpamTrackerModule spamModule,
    GuildLog guildLog,
    GuildIssueReporter issueReporter,
    ILogger<SpamEvents> logger)
    : IEventHandler<MessageCreatedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (args.Author.IsBot || args.Guild is null)
            return;

        if (!await settingsModule.IsModuleEnabled(Module.AntiSpam, args.Guild.GetGuildId())
                .GetOrElse(() => false))
            return;

        var checkSpamResult = await spamModule.CheckSpam(args.Message);

        if (!checkSpamResult.IsSpam)
            return;

        if (args.Author is not DiscordMember member)
            return;

        var guildId = args.Guild.GetGuildId();
        var muteRoleId = await settingsModule.GetEffectiveMuteRole(guildId)
            .GetOrElse(() => null);
        if (muteRoleId is null)
            return;

        // Grant first, so a role Discord refuses, or one that is gone, leaves no mute on record for the expiry task to
        // "lift" later (KI-55). The spam is deleted either way.
        if (await this.TryGrantMuteRoleAsync(args.Guild, member, muteRoleId.Value, checkSpamResult.Reason)
            is not { } muteRole)
        {
            await DeleteSpamAsync(args.Message);
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var muteCount = await dbContext.Sins
            .Where(member1 => member1.UserId == member.GetUserId() && member1.GuildId == guildId)
            .Where(x => x.SinType == SinType.Mute)
            .CountAsync(x => x.SinOn > DateTimeOffset.UtcNow.AddDays(-1));
        var duration = TimeSpan.FromMinutes(Math.Pow(2, muteCount));
        var muteEndTime = DateTimeOffset.UtcNow.Add(duration);
        var now = DateTimeOffset.UtcNow;
        var sin = Sin.ForMute(new ModerationActor.System(), member.GetUserId(), guildId, now,
            ModerationReason.CreateIfNotBlank(checkSpamResult.Reason).Match(r => r, _ => null));

        if (!await this.TryRecordMuteAsync(dbContext, sin, member, new ModeratorId(args.Guild.CurrentMember.Id),
                muteEndTime, muteRoleId.Value))
        {
            // Nothing would ever lift a role with no mute on record.
            await member.RevokeRoleAsync(muteRole, "The mute could not be recorded.");
            await guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = guildId,
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.Red,
                Description =
                    $"Spam from {member.Mention} was caught, but the mute could not be recorded, so it has been undone."
            });
            await DeleteSpamAsync(args.Message);
            return;
        }

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Mute")
            .AddField("User", member.Mention, true)
            .AddField("Sin Id", $"**{sin.Id}**", true)
            .AddField("Moderator", sender.CurrentUser.Mention, true)
            .AddField("Length", $"{duration.TotalMinutes} minute(s)", true)
            .WithColor(GrimoireColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField("Reason", checkSpamResult.Reason);


        await guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guildId, GuildLogType = GuildLogType.Moderation, Embed = embed
        });

        await guildLog.DirectMessageAsync(member, new DiscordEmbedBuilder()
            .WithAuthor($"Mute Id {sin.Id}")
            .WithDescription(
                $"You have been muted for {duration.TotalMinutes} minute(s) by {sender.CurrentUser.Mention} for spamming.")
            .WithColor(GrimoireColor.Red), "mute");

        // Last, so a refusal can't cost the log or the DM.
        await DeleteSpamAsync(args.Message);
    }

    /// <summary>
    ///     Gives the mute role, or reports to the mod log why it couldn't and returns null. Only a refusal or a missing
    ///     role is expected. Anything else, an outage included, throws, and nothing has been recorded yet.
    /// </summary>
    private async Task<DiscordRole?> TryGrantMuteRoleAsync(DiscordGuild guild, DiscordMember member, RoleId muteRoleId,
        string reason)
    {
        if (await guild.GetRoleOrDefaultAsync(muteRoleId) is not { } muteRole)
            return await this.ReportAsync(GuildIssueCatalog.SpamMuteRoleMissing(guild.GetGuildId()));

        try
        {
            await member.GrantRoleAsync(muteRole, reason);
            return muteRole;
        }
        catch (UnauthorizedException)
        {
            return await this.ReportAsync(GuildIssueCatalog.CannotGiveSpamMuteRole(guild.GetGuildId(), muteRoleId));
        }
        catch (NotFoundException)
        {
            return await this.ReportAsync(GuildIssueCatalog.SpamMuteRoleMissing(guild.GetGuildId()));
        }
    }

    private async Task<DiscordRole?> ReportAsync(GuildIssue issue)
    {
        await issueReporter.ReportAsync(issue);
        return null;
    }

    /// <summary>
    ///     Saves the sin and then the mute. On false the caller takes the role back off, so any failure, an outage
    ///     included, must land here and not escape: a role with no mute on record is never lifted.
    /// </summary>
    private async Task<bool> TryRecordMuteAsync(GrimoireDbContext dbContext, Sin sin, DiscordMember member,
        ModeratorId moderatorId, DateTimeOffset muteEndTime, RoleId muteRoleId)
    {
        try
        {
            dbContext.Sins.Add(sin);
            await dbContext.SaveChangesAsync();

            if (await settingsModule.AddMute(member.GetUserId(), member.GetGuildId(), moderatorId, sin.Id,
                    muteEndTime, muteRoleId) is Result<MuteAdded>.Success)
                return true;

            // Take the sin back off, so the member's record doesn't show a mute that never happened.
            dbContext.Sins.Remove(sin);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            LogRecordFailure(logger, ex, member.GetUserId(), member.GetGuildId());
        }

        return false;
    }

    private static async Task DeleteSpamAsync(DiscordMessage message)
    {
        try
        {
            await message.DeleteAsync();
        }
        catch (DiscordException)
        {
            // Without Manage Messages Discord refuses, and PluralKit may have deleted it already. The member is muted
            // or the moderators told either way.
        }
    }

    [LoggerMessage(LogLevel.Error, "Could not record the spam mute of user {UserId} in guild {GuildId}")]
    static partial void LogRecordFailure(ILogger logger, Exception ex, UserId userId, GuildId guildId);
}

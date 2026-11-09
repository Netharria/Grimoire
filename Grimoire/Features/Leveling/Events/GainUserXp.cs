// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Leveling.Events;

public sealed partial class GainUserXp(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog,
    IAlertSender alerts,
    GuildIssueReporter issueReporter,
    ILogger<GainUserXp> logger) : IEventHandler<MessageCreatedEventArgs>
{
    private static readonly Func<GrimoireDbContext, UserId, GuildId, Task<DateTimeOffset?>>
        _getUserXpInfoQuery = EF.CompileAsyncQuery((GrimoireDbContext context, UserId userId, GuildId guildId) =>
            context.XpHistory
                .Where(xp => xp.UserId == userId && xp.GuildId == guildId)
                .Max(history => (DateTimeOffset?)history.TimeOut));

    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly ILogger<GainUserXp> _logger = logger;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
    {
        if (args.Message.MessageType is not DiscordMessageType.Default and not DiscordMessageType.Reply
            || args.Author is not DiscordMember member
            || member.IsBot)
            return;

        if (!await this._settingsModule.IsModuleEnabled(Module.Leveling, member.GetGuildId())
                .GetOrElse(() => false))
            return;

        // If the ignore list can't be read, award nothing: XP given to an ignored member can't be taken back cleanly.
        if (await this._settingsModule.IsMessageIgnored(
                    member.GetGuildId(),
                    args.GetAuthorUserId(),
                    member.Roles.Select(x => x.GetRoleId()).ToHashSet(),
                    args.GetChannelId())
                .GetOrElse(() => true))
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var timeOut = await _getUserXpInfoQuery(dbContext, member.GetUserId(), member.GetGuildId());

        if (timeOut is not null && timeOut > DateTimeOffset.UtcNow)
            return;

        var currentXp = await dbContext.XpHistory
            .Where(entry => entry.UserId == member.GetUserId() && entry.GuildId == member.GetGuildId())
            .SumAsync(entry => entry.RawXp);

        var settingsResult = await this._settingsModule.GetLevelingSettings(member.GetGuildId());
        if (settingsResult is not Result<LevelingSettingEntry>.Success { Value: var levelingSettings })
        {
            // Without settings XP silently stops for the guild, so make it visible.
            var reason = settingsResult.Match(_ => string.Empty, error => error.Code);
            LogSettingsUnavailable(this._logger, member.GetGuildId(), reason);
            alerts.Send(new Alert
            {
                Severity = AlertSeverity.Warning,
                Type = "LevelingSettingsUnavailable",
                Message = $"Could not load leveling settings: {reason}",
                GuildId = member.GetGuildId()
            });
            return;
        }

        var earnedXp = BuildEarnedXp(levelingSettings, args.GetAuthorUserId(), member.GetGuildId());
        await dbContext.XpHistory.AddAsync(earnedXp);
        await dbContext.SaveChangesAsync();

        var previousLevel = levelingSettings.GetLevelFromXp(currentXp);
        var currentLevel = levelingSettings.GetLevelFromXp(currentXp + levelingSettings.Amount.Value);

        if (previousLevel < currentLevel)
            await this.SendLevelUpNotificationAsync(member, currentLevel);

        await this.ApplyRewards(member, currentLevel);
    }

    internal static EarnedXp BuildEarnedXp(LevelingSettingEntry settings, UserId userId, GuildId guildId)
        => PositiveXpAmount.Create(settings.Amount.Value)
            .Map(amount => EarnedXp.Create(amount, userId, guildId,
                DateTimeOffset.UtcNow + settings.XpTimeoutPeriod.Value))
            .Match(e => e, _ => throw new InvariantViolationException());

    private ValueTask SendLevelUpNotificationAsync(DiscordMember member, int currentLevel)
        => this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = member.GetGuildId(),
            GuildLogType = GuildLogType.Leveling,
            Embed = new DiscordEmbedBuilder()
                .WithColor(GrimoireColor.Purple)
                .WithAuthor(member.Username)
                .WithDescription($"{member.Mention} has leveled to level {currentLevel}.")
                .WithFooter($"{member.Id}")
                .WithTimestamp(DateTime.UtcNow)
        });

    private async Task ApplyRewards(DiscordMember member, int userLevel, CancellationToken cancellationToken = default)
    {
        var guild = member.Guild;
        var guildId = guild.GetGuildId();

        // The next XP gain tries again.
        if (await this._settingsModule.GetLevelingRewardsAsync(guildId, cancellationToken)
            is not Result<IReadOnlySet<RewardEntry>>.Success { Value: var rewards })
            return;

        // Nothing else removes a reward whose role was deleted, including while the bot was offline, and a deleted
        // role can't be picked in /Rewards Remove.
        var orphaned = rewards.Where(reward => !guild.Roles.ContainsKey(reward.RoleId.Value)).ToArray();
        if (orphaned.Length > 0)
            await this.RemoveRewardsForDeletedRolesAsync(guild, orphaned, cancellationToken);

        var newRewards = rewards
            .Except(orphaned)
            .Where(reward => reward.RewardLevel <= userLevel)
            .Where(reward => member.Roles.All(role => role.GetRoleId() != reward.RoleId))
            .ToArray();

        if (newRewards.Length == 0)
            return;

        // One role at a time. Replacing the whole role list fails if the member has a managed role, such as Server
        // Booster, undoes changes the cache hasn't seen yet, and lets one role the bot can't grant block the rest.
        List<RewardEntry> granted = [];
        List<RoleId> refused = [];
        foreach (var reward in newRewards)
        {
            if (!guild.Roles.TryGetValue(reward.RoleId.Value, out var role))
                continue;
            try
            {
                await member.GrantRoleAsync(role, "Level reward");
                granted.Add(reward);
            }
            catch (UnauthorizedException)
            {
                refused.Add(reward.RoleId);
            }
        }

        if (refused.Count > 0)
            await issueReporter.ReportAsync(GuildIssueCatalog.RewardRolesNotGrantable(guildId, refused), cancellationToken);

        if (granted.Count == 0)
            return;

        await Task.WhenAll(granted
            .Where(reward => reward.RewardMessage.HasValue)
            .Select(reward => this.SendRewardMessageAsync(guild, member, reward)));

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Leveling,
            Title = member.Username,
            Description = $"{member.Mention} has earned " +
                          $"{string.Join(' ', granted.Select(x => RoleExtensions.Mention(x.RoleId)))}",
            Footer = $"{member.Id}",
            Color = GrimoireColor.DarkPurple
        }, cancellationToken);
    }

    /// <summary>
    ///     Removes the rewards, recorded as removed by the bot, and reports which ones went as a guild issue, so the
    ///     moderators see it with the server's other setup problems.
    /// </summary>
    private async Task RemoveRewardsForDeletedRolesAsync(DiscordGuild guild, RewardEntry[] orphaned,
        CancellationToken cancellationToken)
    {
        List<RewardEntry> removed = [];
        foreach (var reward in orphaned)
            if (await RewardRemoved.Create(reward.RoleId, guild.GetGuildId(), new ModeratorId(guild.CurrentMember.Id),
                        DateTimeOffset.UtcNow)
                    .ToResult()
                    .BindAsync(removal => this._settingsModule.SetRewardAsync(removal, cancellationToken)) is Result<RewardRemoved>.Success)
                removed.Add(reward);

        if (removed.Count > 0)
            await issueReporter.ReportAsync(
                GuildIssueCatalog.RewardsForDeletedRolesRemoved(guild.GetGuildId(), removed), cancellationToken);
    }

    private async Task SendRewardMessageAsync(DiscordGuild guild, DiscordMember member, RewardEntry reward)
    {
        try
        {
            if (guild.Roles.TryGetValue(reward.RoleId.Value, out var role))
                await member.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithAuthor($"Congratulations on earning {role.Name}!", iconUrl: guild.IconUrl)
                    .WithFooter($"Message from the moderators of {guild.Name}.")
                    // Slash options can't hold a newline, so "\n" stands for one. Nothing else is unescaped: a
                    // backslash is often meant, as in \o/, or escapes Markdown.
                    .WithDescription(reward.RewardMessage.GetValueOrDefault().Value.Replace("\\n", "\n", StringComparison.Ordinal)));
        }
        // No token reaches this DM, so a cancellation here can only be a timed-out request.
        catch (Exception ex)
        {
            LogRewardMessageFailure(this._logger, ex, reward.RoleId, reward.RewardMessage?.Value);
        }
    }

    // Closed DMs are common and expected, so this is not a warning.
    [LoggerMessage(LogLevel.Information, "Failure to send reward message Reward: {roleId} Message: {message}")]
    static partial void LogRewardMessageFailure(ILogger logger, Exception ex, RoleId roleId, string? message);

    [LoggerMessage(LogLevel.Warning, "Could not load leveling settings for guild {GuildId}: {Reason}")]
    static partial void LogSettingsUnavailable(ILogger logger, GuildId guildId, string reason);
}

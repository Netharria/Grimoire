// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Leveling.Events;

public sealed partial class GainUserXp(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    DiscordClient client,
    GuildLog guildLog,
    ILogger<EventHandler> logger) : IEventHandler<MessageCreatedEventArgs>
{
    private static readonly Func<GrimoireDbContext, UserId, GuildId, Task<DateTimeOffset?>>
        _getUserXpInfoQuery = EF.CompileAsyncQuery((GrimoireDbContext context, UserId userId, GuildId guildId) =>
            context.XpHistory
                .AsNoTracking()
                .Where(xp => xp.UserId == userId && xp.GuildId == guildId)
                .Max(history => (DateTimeOffset?)history.TimeOut));

    private readonly DiscordClient _client = client;
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly ILogger<EventHandler> _logger = logger;
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

        if (await this._settingsModule.IsMessageIgnored(
                    member.GetGuildId(),
                    args.GetAuthorUserId(),
                    member.Roles.Select(x => x.GetRoleId()).ToHashSet(),
                    args.GetChannelId())
                .GetOrElse(() => false))
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var timeOut = await _getUserXpInfoQuery(dbContext, member.GetUserId(), member.GetGuildId());

        if (timeOut is not null && timeOut > DateTimeOffset.UtcNow)
            return;

        var currentXp = await dbContext.XpHistory
            .AsNoTracking()
            .Where(entry => entry.UserId == member.GetUserId() && entry.GuildId == member.GetGuildId())
            .SumAsync(entry => entry.RawXp);

        if (await this._settingsModule.GetLevelingSettings(member.GetGuildId())
                is not Result<LevelingSettingEntry>.Success { Value: var levelingSettings })
            return;

        var earnedXp = BuildEarnedXp(levelingSettings, args.GetAuthorUserId(), member.GetGuildId());
        await dbContext.XpHistory.AddAsync(earnedXp);
        await dbContext.SaveChangesAsync();

        var previousLevel = levelingSettings.GetLevelFromXp(currentXp);
        var currentLevel = levelingSettings.GetLevelFromXp(currentXp + levelingSettings.Amount.Value);

        if (previousLevel < currentLevel)
            await this.SendLevelUpNotificationAsync(member, currentLevel);

        await this.ApplyRewards(member.GetGuildId(), args.GetAuthorUserId(), currentLevel);
    }

    internal static EarnedXp BuildEarnedXp(LevelingSettingEntry settings, UserId userId, GuildId guildId)
        => PositiveXpAmount.Create(settings.Amount.Value)
            .Bind(amount => EarnedXp.Create(amount, userId, guildId,
                DateTimeOffset.UtcNow + settings.XpTimeoutPeriod.Value))
            .Match(e => e, _ => throw new UnreachableException());

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

    private async Task ApplyRewards(
        GuildId guildId,
        UserId userId,
        int userLevel,
        CancellationToken cancellationToken = default)
    {
        var guild = await this._client.GetGuildOrDefaultAsync(guildId);
        if (guild is null)
            return;
        var member = await guild.GetMemberOrDefaultAsync(userId);
        if (member is null)
            return;

        var rewards = await this._settingsModule.GetLevelingRewardsAsync(guildId, cancellationToken)
            .GetOrElse(() => default!);

        var newRewards = rewards
            .Where(reward => reward.RewardLevel <= userLevel)
            .Where(reward => member.Roles.All(role => role.GetRoleId() != reward.RoleId))
            .ToArray();

        if (newRewards.Length == 0)
            return;

        var rolesToAdd = newRewards
            .Join(guild.Roles,
                reward => reward.RoleId,
                role => role.Value.GetRoleId(),
                (_, role) => role.Value)
            .Concat(member.Roles)
            .Distinct()
            .ToArray();

        try
        {
            await member.ReplaceRolesAsync(rolesToAdd);
        }
        catch (UnauthorizedException)
        {
            await this.SendPermissionErrorLogsAsync(
                guild.CurrentMember.DisplayName,
                newRewards.Select(x => x.RoleId),
                guild.GetGuildId());
        }

        await Task.WhenAll(newRewards
            .Where(reward => reward.RewardMessage.HasValue)
            .Select(reward => this.SendRewardMessageAsync(guild, member, reward)));

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Leveling,
            Title = member.Username,
            Description = $"{member.Mention} has earned " +
                          $"{string.Join(' ', newRewards.Select(x => RoleExtensions.Mention(x.RoleId)))}",
            Footer = $"{member.Id}",
            Color = GrimoireColor.DarkPurple
        }, cancellationToken);
    }

    private async Task SendRewardMessageAsync(DiscordGuild guild, DiscordMember member, RewardEntry reward)
    {
        try
        {
            if (guild.Roles.TryGetValue(reward.RoleId.Value, out var role))
                await member.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithAuthor($"Congratulations on earning {role.Name}!", iconUrl: guild.IconUrl)
                    .WithFooter($"Message from the moderators of {guild.Name}.")
                    .WithDescription(Regex.Unescape(reward.RewardMessage.GetValueOrDefault().Value)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRewardMessageFailure(this._logger, ex, reward.RoleId, reward.RewardMessage?.Value);
        }
    }

    [LoggerMessage(LogLevel.Warning, "Failure to send reward message Reward: {roleId} Message: {message}")]
    static partial void LogRewardMessageFailure(ILogger logger, Exception ex, RoleId roleId, string? message);

    private Task SendPermissionErrorLogsAsync(
        string displayName,
        IEnumerable<RoleId> rewards,
        GuildId guildId)
    {
        var description = $"{displayName} tried to grant roles " +
                          $"{string.Join(' ', rewards.Select(RoleExtensions.Mention))} but did not have sufficient permissions.";

        return Task.WhenAll(
            new[] { GuildLogType.Moderation, GuildLogType.Leveling }.Select(logType =>
                this._guildLog.SendLogMessageAsync(new GuildLogMessage
                {
                    GuildId = guildId,
                    GuildLogType = logType,
                    Description = description,
                    Color = GrimoireColor.Red
                }).AsTask()));
    }
}

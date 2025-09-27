// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Channels.GuildLog;
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
    private static readonly Func<GrimoireDbContext, ulong, ulong, Task<DateTimeOffset?>>
        _getUserXpInfoQuery = EF.CompileAsyncQuery((GrimoireDbContext context, ulong userId, ulong guildId) =>
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
            || args.Guild is null
            || args.Author is not DiscordMember member
            || member.IsBot)
            return;

        if (!await this._settingsModule.IsModuleEnabled(Module.Leveling, args.Guild.Id))
            return;

        if (!await this._settingsModule.IsMessageIgnored(
                args.Guild.Id,
                args.Author.Id,
                member.Roles.Select(x => x.Id).ToArray(),
                args.Channel.Id))
            return;


        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var timeOut = await _getUserXpInfoQuery(dbContext, member.Id, args.Guild.Id);

        if (timeOut is not null && timeOut > DateTimeOffset.UtcNow)
            return;

        var xp = await dbContext.XpHistory
            .AsNoTracking()
            .Where(xp => xp.UserId == member.Id && xp.GuildId == args.Guild.Id)
            .Select(xp => xp.Xp)
            .LongCountAsync();

        var levelingSettingEntry = await this._settingsModule.GetLevelingSettings(args.Guild.Id);

        await dbContext.XpHistory.AddAsync(
            new XpHistory
            {
                Xp = levelingSettingEntry.Amount,
                UserId = args.Author.Id,
                GuildId = args.Guild.Id,
                TimeOut = DateTimeOffset.UtcNow + levelingSettingEntry.TextTime,
                Type = XpHistoryType.Earned
            });
        await dbContext.SaveChangesAsync();

        var previousLevel = levelingSettingEntry.GetLevelFromXp(xp);
        var currentLevel = levelingSettingEntry.GetLevelFromXp(xp + levelingSettingEntry.Amount);

        if (previousLevel < currentLevel)
            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
            {
                GuildId = args.Guild.Id,
                GuildLogType = GuildLogType.Leveling,
                Embed = new DiscordEmbedBuilder()
                    .WithColor(GrimoireColor.Purple)
                    .WithAuthor(member.Username)
                    .WithDescription($"{member.Mention} has leveled to level {currentLevel}.")
                    .WithFooter($"{member.Id}")
                    .WithTimestamp(DateTime.UtcNow)
            });

        await ApplyRewards(args.Guild.Id, args.Author.Id, currentLevel);
    }

    private async Task ApplyRewards(
        ulong guildId,
        ulong userId,
        int userLevel,
        CancellationToken cancellationToken = default)
    {
        var guild = await this._client.GetGuildAsync(guildId);
        var member = await guild.GetMemberAsync(userId);

        var rewards = await this._settingsModule.GetLevelingRewardsAsync(guild.Id, cancellationToken);


        var newRewards = rewards
            .Where(reward => reward.RewardLevel <= userLevel)
            .Where(reward => member.Roles.All(role => role.Id != reward.RoleId))
            .ToArray();

        var rolesToAdd = newRewards
            .Join(guild.Roles,
                reward => reward.RoleId,
                role => role.Key,
                (_, role) => role.Value)
            .Concat(member.Roles)
            .Distinct()
            .ToArray();

        if (newRewards.Length == 0)
            return;

        try
        {
            await member.ReplaceRolesAsync(rolesToAdd);
        }
        catch (UnauthorizedException)
        {
            await SendErrorLogs(
                guild.CurrentMember.DisplayName,
                newRewards.Select(x => x.RoleId),
                guild.Id);
        }

        foreach (var reward in newRewards.Where(reward => !string.IsNullOrWhiteSpace(reward.RewardMessage)))
            try
            {
                if (guild.Roles.TryGetValue(reward.RoleId, out var role))
                    await member.SendMessageAsync(new DiscordEmbedBuilder()
                        .WithAuthor($"Congratulations on earning {role.Name}!", iconUrl: guild.IconUrl)
                        .WithFooter($"Message from the moderators of {guild.Name}.")
                        .WithDescription(Regex.Unescape(reward.RewardMessage!)));
            }
            catch (Exception ex)
            {
                LogRewardMessageFailure(this._logger, ex, reward.RoleId, reward.RewardMessage);
            }

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.Id,
            GuildLogType = GuildLogType.Leveling,
            Title = member.Username,
            Description = $"{member.Mention} has earned " +
                          $"{string.Join(' ', newRewards.Select(x => RoleExtensions.Mention(x.RoleId)))}",
            Footer = $"{member.Id}",
            Color = GrimoireColor.DarkPurple
        }, cancellationToken);
    }

    [LoggerMessage(LogLevel.Warning, "Failure to send reward message Reward: {roleId} Message: {message}")]
    static partial void LogRewardMessageFailure(ILogger logger, Exception ex, ulong roleId, string? message);

    private async Task SendErrorLogs(
        string displayName,
        IEnumerable<ulong> rewards,
        ulong guildId)
    {
        var roleString = string.Join(' ', rewards.Select(RoleExtensions.Mention));

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guildId,
            GuildLogType = GuildLogType.Moderation,
            Description = $"{displayName} tried to grant roles " +
                          $"{roleString} but did not have sufficient permissions.",
            Color = GrimoireColor.Red
        });

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guildId,
            GuildLogType = GuildLogType.Leveling,
            Description = $"{displayName} tried to grant roles " +
                          $"{roleString} but did not have sufficient permissions.",
            Color = GrimoireColor.Red
        });
    }

    public sealed record QueryResult
    {
        public required long Xp { get; init; }
        public required DateTimeOffset? Timeout { get; init; }
    }
}

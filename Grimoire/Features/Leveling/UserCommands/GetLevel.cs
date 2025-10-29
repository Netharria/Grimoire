// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Leveling.UserCommands;

[RequireGuild]
[RequireModuleEnabled(Module.Leveling)]
public sealed class GetLevel(IDbContextFactory<GrimoireDbContext> dbContextFactory, SettingsModule settingsModule)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Level")]
    [Description("Gets the leveling details for the user.")]
    public async Task LevelAsync(
        CommandContext ctx,
        [Parameter("user")] [Description("User to get details from. Blank will return your info.")]
        DiscordUser? user = null)
    {
        if (ctx.Guild is null || ctx.Member is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "This command can only be used in a server.");
            return;
        }

        if (!await this._settingsModule.IsModuleEnabled(Module.Leveling, ctx.Guild.Id))
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "The leveling module is not enabled on this server.");
            return;
        }

        var userCommandChannelId = await this._settingsModule.GetUserCommandChannel(ctx.Guild.Id);

        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(
                !ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                && userCommandChannelId != ctx.Channel.Id);
        else if (!ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                 && userCommandChannelId != ctx.Channel.Id)
            return;


        user ??= ctx.User;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var membersXp = await dbContext.XpHistory
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.GuildId == ctx.Guild.Id)
            .GroupBy(x => new { x.UserId, x.GuildId })
            .Select(xpHistories => xpHistories.Sum(x => x.Xp))
            .FirstOrDefaultAsync();

        var levelingSettings = await this._settingsModule.GetLevelingSettings(ctx.Guild.Id);

        var currentLevel = levelingSettings.GetLevelFromXp(membersXp);
        var currentLevelXp = levelingSettings.GetXpNeededForLevel(currentLevel);
        var nextLevelXp = levelingSettings.GetXpNeededForLevel(currentLevel, 1);

        var rewards = await this._settingsModule.GetLevelingRewardsAsync(ctx.Guild.Id);

        var nextReward = rewards.FirstOrDefault(reward => reward.RewardLevel > currentLevel);

        DiscordColor color;
        string displayName;
        string avatarUrl;

        if (user is DiscordMember member)
        {
            color = member.Color.PrimaryColor;
            displayName = member.DisplayName;
            avatarUrl = member.GetGuildAvatarUrl(MediaFormat.Auto);
        }
        else
        {
            color = user.BannerColor ?? DiscordColor.Blurple;
            displayName = user.Username;
            avatarUrl = user.GetAvatarUrl(MediaFormat.Auto);
        }

        if (string.IsNullOrEmpty(avatarUrl))
            avatarUrl = user.DefaultAvatarUrl;


        DiscordRole? roleReward = null;
        if (nextReward is not null)
            roleReward = await ctx.Guild.GetRoleAsync(nextReward.RoleId);

        var embed = new DiscordEmbedBuilder()
            .WithColor(color)
            .WithTitle($"Level and EXP for {displayName}")
            .AddField("XP", $"{membersXp}", true)
            .AddField("Level", $"{currentLevel}", true)
            .AddField("Progress", $"{membersXp - currentLevelXp}/{nextLevelXp - currentLevelXp}", true)
            .AddField("Next Reward",
                roleReward is null ? "None" : $"{roleReward.Mention}\n at level {nextLevelXp}", true)
            .WithThumbnail(avatarUrl)
            .WithFooter($"{ctx.Guild.Name}", ctx.Guild.IconUrl)
            .Build();
        await ctx.EditReplyAsync(embed: embed);
    }
}

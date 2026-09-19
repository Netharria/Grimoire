// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Settings.Domain;
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
        if (ctx.Guild is not { } guild || ctx.Member is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "This command can only be used in a server.");
            return;
        }

        if (!await this._settingsModule.IsModuleEnabled(Module.Leveling, guild.GetGuildId()).GetOrElse(() => false))
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "The leveling module is not enabled on this server.");
            return;
        }

        var userCommandChannelId = await this._settingsModule.GetUserCommandChannel(guild.GetGuildId());

        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(
                !ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                && userCommandChannelId.GetOrElse(() => null) != ctx.GetChannelId());
        else if (!ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                 && userCommandChannelId.GetOrElse(() => null) != ctx.GetChannelId())
            return;

        user ??= ctx.User;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var membersXp = await dbContext.XpHistory
            .AsNoTracking()
            .Where(x => x.UserId == user.GetUserId() && x.GuildId == guild.GetGuildId())
            .SumAsync(x => x.RawXp);

        if (await this._settingsModule.GetLevelingSettings(guild.GetGuildId())
                is not Result<LevelingSettingEntry>.Success { Value: var levelingSettings })
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "Could not load leveling settings. Please try again later.");
            return;
        }

        var currentLevel = levelingSettings.GetLevelFromXp(membersXp);
        var currentLevelXp = levelingSettings.GetXpNeededForLevel(currentLevel);
        var nextLevelXp = levelingSettings.GetXpNeededForLevel(currentLevel, 1);

        var rewards = await this._settingsModule.GetLevelingRewardsAsync(guild.GetGuildId())
            .GetOrElse(() => default!);
        var nextReward = rewards.FirstOrDefault(reward => reward.RewardLevel > currentLevel);

        var (color, displayName, avatarUrl) = user is DiscordMember member
            ? (member.Color.PrimaryColor, member.DisplayName, member.GetGuildAvatarUrl(MediaFormat.Auto))
            : (user.BannerColor ?? DiscordColor.Blurple, user.Username, user.GetAvatarUrl(MediaFormat.Auto));

        if (string.IsNullOrEmpty(avatarUrl))
            avatarUrl = user.DefaultAvatarUrl;

        var roleReward = nextReward is not null
            ? await guild.GetRoleOrDefaultAsync(nextReward.RoleId)
            : null;

        var embed = new DiscordEmbedBuilder()
            .WithColor(color)
            .WithTitle($"Level and EXP for {displayName}")
            .AddField("XP", $"{membersXp}", true)
            .AddField("Level", $"{currentLevel}", true)
            .AddField("Progress", $"{membersXp - currentLevelXp}/{nextLevelXp - currentLevelXp}", true)
            .AddField("Next Reward",
                roleReward is null ? "None" : $"{roleReward.Mention}\n at level {nextLevelXp}", true)
            .WithThumbnail(avatarUrl)
            .WithFooter($"{guild.Name}", guild.IconUrl)
            .Build();
        await ctx.ReplyAsync(embed: embed);
    }
}

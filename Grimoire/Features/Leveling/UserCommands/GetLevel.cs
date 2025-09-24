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

public sealed class GetLevel
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    internal sealed class Command(IDbContextFactory<GrimoireDbContext> dbContextFactory, SettingsModule settingsModule)
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
        private readonly SettingsModule _settingsModule = settingsModule;

        [Command("Level")]
        [Description("Gets the leveling details for the user.")]
        public async Task LevelAsync(
            SlashCommandContext ctx,
            [Parameter("user")] [Description("User to get details from. Blank will return your info.")]
            DiscordUser? user = null)
        {
            if (ctx.Guild is null || ctx.Member is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var guildSettings = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);

            await ctx.DeferResponseAsync(!ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                                         && guildSettings.UserCommandChannelId != ctx.Channel.Id);
            user ??= ctx.User;

            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
            var membersXp = await dbContext.XpHistory
                .AsNoTracking()
                .Where(x => x.UserId == user.Id && x.GuildId == ctx.Guild.Id)
                .GroupBy(x => new { x.UserId, x.GuildId })
                .Select(xpHistories => xpHistories.Sum(x => x.Xp))
                .FirstOrDefaultAsync();

            var currentLevel = guildSettings.LevelSettings.GetLevelFromXp(membersXp);
            var currentLevelXp = guildSettings.LevelSettings.GetXpNeededForLevel(currentLevel);
            var nextLevelXp = guildSettings.LevelSettings.GetXpNeededForLevel(currentLevel, 1);

            var nextReward = guildSettings.Rewards.FirstOrDefault(reward => reward.RewardLevel > currentLevel);

            DiscordColor color;
            string displayName;
            string avatarUrl;

            if (user is DiscordMember member)
            {
                color = member.Color;
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
                .AddField("Progress", $"{membersXp - currentLevelXp}/{currentLevelXp - nextLevelXp}", true)
                .AddField("Next Reward",
                    roleReward is null ? "None" : $"{roleReward.Mention}\n at level {nextLevelXp}", true)
                .WithThumbnail(avatarUrl)
                .WithFooter($"{ctx.Guild.Name}", ctx.Guild.IconUrl)
                .Build();
            await ctx.EditReplyAsync(embed: embed);
        }
    }
}

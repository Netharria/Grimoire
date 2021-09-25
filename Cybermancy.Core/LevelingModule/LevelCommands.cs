// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Core.Enums;
using Cybermancy.Core.Extensions;
using Cybermancy.Domain;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;

namespace Cybermancy.Core.LevelingModule
{
    [SlashRequireGuild]
    public class LevelCommands : ApplicationCommandModule
    {
        private readonly IUserLevelService _userLevelService;
        private readonly IRewardService _rewardService;

        /// <summary>
        /// Initializes a new instance of the <see cref="LevelCommands"/> class.
        /// </summary>
        /// <param name="userLevelService"></param>
        /// <param name="rewardService"></param>
        public LevelCommands(IUserLevelService userLevelService, IRewardService rewardService)
        {
            this._userLevelService = userLevelService;
            this._rewardService = rewardService;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="user"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [SlashCommand("Level", "Gets the leveling details for the user.")]
        public async Task LevelAsync(
            InteractionContext ctx,
            [Option("user", "User to get details from. Blank will return your info.")] DiscordUser user = null)
        {
            user ??= ctx.User;
            var userLevel = await this._userLevelService.GetUserLevelAsync(user.Id, ctx.Guild.Id);
            if (userLevel is null)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "That user could not be found.");
                return;
            }

            if (user is not DiscordMember member) return;
            var level = userLevel.GetLevel();
            var rewards = await this._rewardService.GetAllGuildRewardsAsync(ctx.Guild.Id);
            Reward nextReward = null;
            foreach (var reward in rewards)
            {
                if (reward.RewardLevel <= level)
                {
                    continue;
                }
                else if (nextReward is null || reward.RewardLevel < nextReward.RewardLevel)
                {
                    nextReward = reward;
                }
            }

            DiscordRole roleReward = null;
            if (nextReward is not null)
            {
                roleReward = ctx.Guild.GetRole(nextReward.Role.Id);
            }

            var levelProgress = userLevel.Xp - userLevel.GetXpNeeded();
            var xpBetween = userLevel.GetXpNeeded(1) - userLevel.GetXpNeeded();
            var embed = new DiscordEmbedBuilder()
                .WithColor(member.Color)
                .WithTitle($"Level and EXP for {member.DisplayName}")
                .AddField("XP", $"{userLevel.Xp}", inline: true)
                .AddField("Level", $"{level}", inline: true)
                .AddField("Progress", $"{levelProgress}/{xpBetween}", inline: true)
                .AddField("Next Reward", roleReward is null ? "None" : $"{roleReward.Mention}\n at level {nextReward.RewardLevel}", inline: true)
                .WithThumbnail(member.AvatarUrl ?? member.DefaultAvatarUrl)
                .WithFooter($"{ctx.Guild.Name}", ctx.Guild.IconUrl)
                .Build();
            await ctx.ReplyAsync(
                embed: embed,
                ephemeral: !member.Permissions.HasPermission(Permissions.ManageMessages));
        }
    }
}

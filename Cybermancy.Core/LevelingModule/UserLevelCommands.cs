using Cybermancy.Core.Extensions;
using Cybermancy.Core.Services;
using Cybermancy.Core.Enums;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Domain;

namespace Cybermancy.Core.LevelingModule
{
    public class UserLevelCommands : ApplicationCommandModule
    {
        public IUserLevelService _userLevelService;
        public IRewardService _rewardService;
        public UserLevelCommands(IUserLevelService userLevelService, IRewardService rewardService)
        {
            _userLevelService = userLevelService;
            _rewardService = rewardService;
        }
        [SlashCommand("level", "Gets the leveling details for the user.")]
        public async Task Level(InteractionContext ctx, 
            [Option("user", "User to get details from. Blank will return your info.")] DiscordUser user = null)
        {
            user ??= ctx.User;
            var userLevel = await _userLevelService.GetUserLevels(user.Id, ctx.Guild.Id);
            if (userLevel is null)
            {
                await ctx.Reply(CybermancyColor.Orange, message: "That user could not be found.");
                return;
            }
            if (user is not DiscordMember member) return;
            var level = userLevel.GetLevel();
            var rewards = await _rewardService.GetAllGuildRewards(ctx.Guild.Id);
            Reward nextReward = null;
            foreach(var reward in rewards)
            {
                if (reward.RewardLevel <= level) continue;
                else if (nextReward is null || reward.RewardLevel < nextReward.RewardLevel) nextReward = reward;
            }
            DiscordRole roleReward = null;
            if(nextReward is not null) roleReward = ctx.Guild.GetRole(nextReward.Role.Id);
            var levelProgress = userLevel.Xp = userLevel.GetXpNeeded();
            var xpBetween = userLevel.GetXpNeeded(1) - userLevel.GetXpNeeded();
            var embed = new DiscordEmbedBuilder()
                .WithColor(member.Color)
                .WithTitle($"Level and EXP for {member.DisplayName}")
                .AddField("XP", $"{userLevel.Xp}", true)
                .AddField("Level", $"{level}", true)
                .AddField("Progress", $"{levelProgress}/{xpBetween}", true)
                .AddField("Next Reward", roleReward is null ? "None" : $"{roleReward.Mention}\n at level {nextReward.RewardLevel}", true)
                .WithThumbnail(member.AvatarUrl ?? member.DefaultAvatarUrl)
                .WithFooter($"{ctx.Guild.Name}", ctx.Guild.IconUrl)
                .Build();
            await ctx.Reply(embed: embed);

        }
    }
}

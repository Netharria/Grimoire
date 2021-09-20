using Cybermancy.Core.Contracts.Services;
using Cybermancy.Core.Enums;
using Cybermancy.Core.Extensions;
using Cybermancy.Domain;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cybermancy.Core.LevelingModule
{

    [SlashCommandGroup("Leaderboard", "Posts the leaderboard for the server.")]
    [SlashRequireGuild]
    public class LeaderboardCommands : ApplicationCommandModule
    {
        public IUserLevelService _userLevelService;
        public IRewardService _rewardService;
        public LeaderboardCommands(IUserLevelService userLevelService, IRewardService rewardService)
        {
            _userLevelService = userLevelService;
            _rewardService = rewardService;
        }

        [SlashCommand("Me", "Find out where you are on the Leaderboard.")]
        public async Task Me(InteractionContext ctx)
        {
            var guildRankedUsers = await _userLevelService.GetRankedUsers(ctx.Guild.Id);
            var requestedUser = guildRankedUsers.FirstOrDefault(x => x.UserId == ctx.User.Id);
            if (requestedUser is null)
            {
                await ctx.Reply(CybermancyColor.Orange, message: "That user could not be found.", footer: $"Total Users {guildRankedUsers.Count}");
                return;
            }
            var leaderboardText = new StringBuilder();
            var userIndex = guildRankedUsers.IndexOf(requestedUser);
            int startIndex;
            if (userIndex - 5 < 0)
                startIndex = 0;
            else
                startIndex = userIndex - 5;
            for (var i = 0; i < 10 && i < guildRankedUsers.Count && startIndex < guildRankedUsers.Count; i++)
            {
                var retrievedUser = await ctx.Client.GetUserAsync(guildRankedUsers[i].UserId);
                if (retrievedUser is not null)
                    leaderboardText.Append($"**{i + 1}** {retrievedUser.Mention} **XP:** {guildRankedUsers[i].Xp}\n");
                startIndex++;
            }
            await ctx.Reply(CybermancyColor.Gold,
                title: "LeaderBoard",
                message: leaderboardText.ToString(),
                footer: $"Total Users {guildRankedUsers.Count}",
                ephemeral: !(ctx.User as DiscordMember).Permissions.HasPermission(Permissions.ManageMessages));
        }

        [SlashCommand("User", "Find out where someone are on the Leaderboard.")]
        public async Task User(InteractionContext ctx, [Option("User", "User to find on the leaderboard")] DiscordUser user)
        {
            var guildRankedUsers = await _userLevelService.GetRankedUsers(ctx.Guild.Id);
            var requestedUser = guildRankedUsers.FirstOrDefault(x => x.UserId == user.Id);
            if (requestedUser is null)
            {
                await ctx.Reply(CybermancyColor.Orange, message: "That user could not be found.", footer: $"Total Users {guildRankedUsers.Count}");
                return;
            }
            var leaderboardText = new StringBuilder();
            var userIndex = guildRankedUsers.IndexOf(requestedUser);
            int startIndex;
            if (userIndex - 5 < 0)
                startIndex = 0;
            else
                startIndex = userIndex - 5;
            for (var i = 0; i < 10 && i < guildRankedUsers.Count && startIndex < guildRankedUsers.Count; i++)
            {
                var retrievedUser = await ctx.Client.GetUserAsync(guildRankedUsers[i].UserId);
                if (retrievedUser is not null)
                    leaderboardText.Append($"**{i + 1}** {retrievedUser.Mention} **XP:** {guildRankedUsers[i].Xp}\n");
                startIndex++;
            }
            await ctx.Reply(CybermancyColor.Gold,
                title: "LeaderBoard",
                message: leaderboardText.ToString(),
                footer: $"Total Users {guildRankedUsers.Count}",
                ephemeral: !(ctx.User as DiscordMember).Permissions.HasPermission(Permissions.ManageMessages));
        }

        [SlashCommand("All", "Get the top xp earners for the server.")]
        public async Task All(InteractionContext ctx)
        {
            var guildRankedUsers = await _userLevelService.GetRankedUsers(ctx.Guild.Id);
            var leaderboardText = await BuildLeaderboardText(ctx, guildRankedUsers);

            var interactivity = ctx.Client.GetInteractivity();
            var embed = new DiscordEmbedBuilder()
                .WithTitle("LeaderBoard")
                .WithFooter($"Total Users {guildRankedUsers.Count}");
            var embedPages = interactivity.GeneratePagesInEmbed(leaderboardText.ToString(), SplitType.Line, embed);
            var result = !(ctx.User as DiscordMember).Permissions.HasPermission(Permissions.ManageMessages);
            await interactivity.SendPaginatedResponseAsync(
                interaction: ctx.Interaction,
                ephemeral: !(ctx.User as DiscordMember).Permissions.HasPermission(Permissions.ManageMessages),
                user: ctx.User, 
                pages: embedPages);
        }

        private async static Task<string> BuildLeaderboardText(InteractionContext ctx, IList<UserLevel> guildRankedUsers)
        {
            var leaderboardText = new StringBuilder();
            foreach (var (user, i) in guildRankedUsers.Select((x, i) => (x, i)))
            {
                var retrievedUser = await ctx.Client.GetUserAsync(user.UserId);
                if (retrievedUser is not null)
                    leaderboardText.Append($"**{i + 1}** {retrievedUser.Mention} **XP:** {user.Xp}\n");
            }
            return leaderboardText.ToString();
        }
    }
}

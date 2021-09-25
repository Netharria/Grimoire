// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

namespace Cybermancy.Core.LevelingModule
{
    /// <summary>
    /// Slash Commands for leaderboard commands.
    /// </summary>
    [SlashCommandGroup("Leaderboard", "Posts the leaderboard for the server.")]
    [SlashRequireGuild]
    public class LeaderboardCommands : ApplicationCommandModule
    {
        private readonly IUserLevelService _userLevelService;

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaderboardCommands"/> class.
        /// </summary>
        /// <param name="userLevelService">The service for managing getting the <see cref="UserLevel"/> from the database.</param>
        public LeaderboardCommands(IUserLevelService userLevelService)
        {
            this._userLevelService = userLevelService;
        }

        /// <summary>
        /// Gets where the user who called it is on the leaderboard as well as the people imediately before and after them.
        /// </summary>
        /// <param name="ctx">The context which initiated the interaction.</param>
        /// <returns>The completed task.</returns>
        [SlashCommand("Me", "Find out where you are on the Leaderboard.")]
        public async Task MeAsync(InteractionContext ctx)
        {
            var guildRankedUsers = await this._userLevelService.GetRankedUsersAsync(ctx.Guild.Id);
            var requestedUser = guildRankedUsers.FirstOrDefault(x => x.UserId == ctx.User.Id);
            if (requestedUser is null)
            {
                await ctx.ReplyAsync(color: CybermancyColor.Orange, message: "That user could not be found.", footer: $"Total Users {guildRankedUsers.Count}");
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
                    leaderboardText.Append("**").Append(i + 1).Append("** ").Append(retrievedUser.Mention).Append(" **XP:** ").Append(guildRankedUsers[i].Xp).Append('\n');
                startIndex++;
            }

            await ctx.ReplyAsync(
                color: CybermancyColor.Gold,
                title: "LeaderBoard",
                message: leaderboardText.ToString(),
                footer: $"Total Users {guildRankedUsers.Count}",
                ephemeral: !(ctx.User as DiscordMember).Permissions.HasPermission(Permissions.ManageMessages));
        }

        /// <summary>
        /// Gets where a specific user is on the leaderboard as well as the people imediately before and after them.
        /// </summary>
        /// <param name="ctx">The context which initiated the interaction.</param>
        /// <param name="user">The user to find on the leaderboard.</param>
        /// <returns>The completed task.</returns>
        [SlashCommand("User", "Find out where someone are on the Leaderboard.")]
        public async Task UserAsync(InteractionContext ctx, [Option("User", "User to find on the leaderboard")] DiscordUser user)
        {
            var guildRankedUsers = await this._userLevelService.GetRankedUsersAsync(ctx.Guild.Id);
            var requestedUser = guildRankedUsers.FirstOrDefault(x => x.UserId == user.Id);
            if (requestedUser is null)
            {
                await ctx.ReplyAsync(color: CybermancyColor.Orange, message: "That user could not be found.", footer: $"Total Users {guildRankedUsers.Count}");
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
                    leaderboardText.Append("**").Append(i + 1).Append("** ").Append(retrievedUser.Mention).Append(" **XP:** ").Append(guildRankedUsers[i].Xp).Append('\n');
                startIndex++;
            }

            await ctx.ReplyAsync(
                color: CybermancyColor.Gold,
                title: "LeaderBoard",
                message: leaderboardText.ToString(),
                footer: $"Total Users {guildRankedUsers.Count}",
                ephemeral: !(ctx.User as DiscordMember).Permissions.HasPermission(Permissions.ManageMessages));
        }

        /// <summary>
        /// Gets the top of the leaderboard in a paginated format so that people can scroll through it.
        /// </summary>
        /// <param name="ctx">The context which initiated the interaction.</param>
        /// <returns>The completed task.</returns>
        [SlashCommand("All", "Get the top xp earners for the server.")]
        public async Task AllAsync(InteractionContext ctx)
        {
            var guildRankedUsers = await this._userLevelService.GetRankedUsersAsync(ctx.Guild.Id);
            var leaderboardText = await BuildLeaderboardTextAsync(ctx, guildRankedUsers);
            var interactivity = ctx.Client.GetInteractivity();
            var embed = new DiscordEmbedBuilder()
                .WithTitle("LeaderBoard")
                .WithFooter($"Total Users {guildRankedUsers.Count}");
            var embedPages = interactivity.GeneratePagesInEmbed(input: leaderboardText, SplitType.Line, embed);
            await interactivity.SendPaginatedResponseAsync(interaction: ctx.Interaction, ephemeral: !(ctx.User as DiscordMember).Permissions.HasPermission(Permissions.ManageMessages), user: ctx.User, pages: embedPages);
        }

        private static async Task<string> BuildLeaderboardTextAsync(InteractionContext ctx, IList<UserLevel> guildRankedUsers)
        {
            var leaderboardText = new StringBuilder();
            foreach (var (user, i) in guildRankedUsers.Select((x, i) => (x, i)))
            {
                var retrievedUser = await ctx.Client.GetUserAsync(user.UserId);
                if (retrievedUser is not null)
                    leaderboardText.Append("**").Append(i + 1).Append("** ").Append(retrievedUser.Mention).Append(" **XP:** ").Append(user.Xp).Append('\n');
            }

            return leaderboardText.ToString();
        }
    }
}

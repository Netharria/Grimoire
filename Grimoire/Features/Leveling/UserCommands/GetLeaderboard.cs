// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Leveling.UserCommands;

public sealed class GetLeaderboard
{
    public enum LeaderboardOption
    {
        [ChoiceDisplayName("Top")] Top,
        [ChoiceDisplayName("Me")] Me,
        [ChoiceDisplayName("User")] User
    }

    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    public sealed class Command(IDbContextFactory<GrimoireDbContext> dbContextFactory, SettingsModule settingsModule)
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
        private readonly SettingsModule _settingsModule = settingsModule;

        [Command("Leaderboard")]
        [Description("Posts the leaderboard for the server.")]
        public async Task LeaderboardAsync(SlashCommandContext ctx,
            [Parameter("Option")]
            [Description("Select either to view the top users, your position, or a specific user.")]
            LeaderboardOption option,
            [Parameter("User")] [Description("The user to find on the leaderboard.")]
            DiscordUser? user = null)
        {
            if (ctx.Guild is null || ctx.Member is null)
                throw new AnticipatedException("This command can only be used in a server.");
            switch (option)
            {
                case LeaderboardOption.Top:
                    user = null;
                    break;
                case LeaderboardOption.Me:
                    user = ctx.User;
                    break;
                case LeaderboardOption.User:
                    if (user is null)
                        throw new AnticipatedException("Must provide a user for this option.");
                    break;
            }

            var guildSettings = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);

            await ctx.DeferResponseAsync(!ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                                         && guildSettings.UserCommandChannelId != ctx.Channel.Id);

            var getUserCenteredLeaderboardQuery = new Request { UserId = user?.Id, GuildId = ctx.Guild.Id };

            var response = await Handle(getUserCenteredLeaderboardQuery, CancellationToken.None);

            await ctx.EditReplyAsync(
                GrimoireColor.DarkPurple,
                title: "LeaderBoard",
                message: response.LeaderboardText,
                footer: $"Total Users {response.TotalUserCount}");
        }

        public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var query = dbContext.XpHistory
                .AsNoTracking()
                .Where(xpHistory => xpHistory.GuildId == request.GuildId)
                .GroupBy(xpHistory => new { xpHistory.UserId, xpHistory.GuildId })
                .Select(group => new { group.Key.UserId, Xp = group.Sum(xpHistory => xpHistory.Xp) })
                .OrderByDescending(xpHistory => xpHistory.Xp);

            if (request.UserId is null)
            {
                var rankedMembers = await query
                    .Take(15)
                    .ToArrayAsync(cancellationToken);

                var totalMemberCount = await dbContext.Members
                    .AsNoTracking()
                    .Where(x => x.GuildId == request.GuildId)
                    .CountAsync(cancellationToken);

                var leaderboardText = new StringBuilder();
                for (var i = 0; i < 15 && i < totalMemberCount; i++)
                    leaderboardText.Append(
                        $"**{i + 1}** {UserExtensions.Mention(rankedMembers[i].UserId)} **XP:** {rankedMembers[i].Xp}\n");
                return new Response { LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalMemberCount };
            }
            else
            {
                var rankedMembers = await query.ToArrayAsync(cancellationToken);

                var totalMemberCount = rankedMembers.Length;

                var memberPosition = Array.FindIndex(rankedMembers, x => x.UserId == request.UserId);

                if (memberPosition == -1)
                    throw new AnticipatedException("Could not find user on leaderboard.");

                var startIndex = Math.Max(0, memberPosition - 5);
                startIndex = Math.Min(startIndex, totalMemberCount - 15);
                startIndex = Math.Max(0, startIndex);

                var leaderboardText = new StringBuilder();
                for (var i = 0; i < 15 && startIndex < totalMemberCount; i++, startIndex++)
                    leaderboardText.Append(
                        $"**{startIndex + 1}** <@!{rankedMembers[startIndex].UserId}> **XP:** {rankedMembers[startIndex].Xp}\n");

                return new Response { LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalMemberCount };
            }
        }
    }

    public sealed record Request
    {
        public required ulong GuildId { get; init; }
        public ulong? UserId { get; init; }
    }

    public sealed record Response
    {
        public required string LeaderboardText { get; init; }
        public required int TotalUserCount { get; init; }
    }
}

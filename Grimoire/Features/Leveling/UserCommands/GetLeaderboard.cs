// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using LanguageExt;
using LanguageExt.Common;

namespace Grimoire.Features.Leveling.UserCommands;

[RequireGuild]
[RequireModuleEnabled(Module.Leveling)]
public sealed class GetLeaderboard(IDbContextFactory<GrimoireDbContext> dbContextFactory, SettingsModule settingsModule)
{
    public enum LeaderboardOption
    {
        [ChoiceDisplayName("Top")] Top,
        [ChoiceDisplayName("Me")] Me,
        [ChoiceDisplayName("User")] User
    }

    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Leaderboard")]
    [Description("Posts the leaderboard for the server.")]
    public async Task LeaderboardAsync(CommandContext ctx,
        [Parameter("Option")] [Description("Select either to view the top users, your position, or a specific user.")]
        LeaderboardOption option,
        [Parameter("User")] [Description("The user to find on the leaderboard.")]
        DiscordUser? user = null)
    {
        var guild = ctx.Guild!;
        var member = ctx.Member!;
        var targetUser = option switch
        {
            LeaderboardOption.Top => null,
            LeaderboardOption.Me => ctx.User,
            LeaderboardOption.User => user,
            _ => throw new UnreachableException()
        };

        if (option == LeaderboardOption.User && user is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "You must specify a user when selecting the 'User' option.");
            return;
        }

        var userCommandChannel = await this._settingsModule.GetUserCommandChannel(guild.GetGuildId());

        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(
                !member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                && userCommandChannel != ctx.GetChannelId());
        else if (!ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                 && userCommandChannel != ctx.GetChannelId())
            return;

        var getUserCenteredLeaderboardQuery = new Request { UserId = targetUser?.GetUserId(), GuildId = guild.GetGuildId() };

        var response = await Handle(getUserCenteredLeaderboardQuery, CancellationToken.None);
        await response
            .Match(
                success => ctx.EditReplyAsync(
                        GrimoireColor.DarkPurple,
                        title: "LeaderBoard",
                        message: success.LeaderboardText,
                        footer: $"Total Users {success.TotalUserCount}"),
             error => ctx.EditReplyAsync(GrimoireColor.Yellow, error.Message));
    }

    private async Task<Fin<Response>> Handle(Request request, CancellationToken cancellationToken)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (request.UserId is null)
        {
            var rankedMembers = await dbContext.LeaderboardView
                .AsNoTracking()
                .Where(x => x.GuildId == request.GuildId)
                .OrderBy(x => x.Rank)
                .Take(15)
                .ToArrayAsync(cancellationToken);

            var totalMemberCount = await dbContext.LeaderboardView
                .Where(x => x.GuildId == request.GuildId)
                .CountAsync(cancellationToken);

            var leaderboardText = new StringBuilder();
            foreach (var rankedMember in rankedMembers)
                leaderboardText.AppendLine(
                    $"**{rankedMember.Rank}** {UserExtensions.Mention(rankedMember.UserId)} **XP:** {rankedMember.TotalXp}");
            return new Response { LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalMemberCount };
        }
        else
        {
            var userEntry = await dbContext.Set<LeaderboardView>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.GuildId == request.GuildId && x.UserId == request.UserId,
                    cancellationToken);

            if (userEntry is null)
                return Error.New("User not found on the leaderboard.");

            var surroundingUsers = await dbContext.Set<LeaderboardView>()
                .AsNoTracking()
                .Where(x => x.GuildId == request.GuildId &&
                            x.Rank >= userEntry.Rank - 5 &&
                            x.Rank <= userEntry.Rank + 9)
                .OrderBy(x => x.Rank)
                .ToArrayAsync(cancellationToken);

            var totalCount = await dbContext.Set<LeaderboardView>()
                .Where(x => x.GuildId == request.GuildId)
                .CountAsync(cancellationToken);


            var leaderboardText = new StringBuilder();
            foreach (var member in surroundingUsers)
                leaderboardText.AppendLine(
                    $"**{member.Rank}** {UserExtensions.Mention(member.UserId)} **XP:** {member.TotalXp}");

            return new Response { LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalCount };
        }
    }

    private sealed record Request
    {
        public required GuildId GuildId { get; init; }
        public UserId? UserId { get; init; }
    }

    private sealed record Response
    {
        public required string LeaderboardText { get; init; }
        public required int TotalUserCount { get; init; }
    }
}

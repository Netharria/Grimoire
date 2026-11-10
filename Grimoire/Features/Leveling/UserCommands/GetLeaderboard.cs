// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

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
        [Parameter("option")] [Description("Select either to view the top users, your position, or a specific user.")]
        LeaderboardOption option,
        [Parameter("user")] [Description("The user to find on the leaderboard.")]
        DiscordUser? user = null)
    {
        var guild = ctx.RequiredGuild;
        var member = ctx.RequiredMember;

        if (option == LeaderboardOption.User && user is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "You must specify a user when selecting the 'User' option.");
            return;
        }

        var userCommandChannel = await this._settingsModule.GetUserCommandChannel(guild.GetGuildId());

        if (!await ctx.TryDeferAsync(!member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                                     && userCommandChannel.GetOrElse(() => null) != ctx.GetChannelId()))
            return;

        var targetUserId = option switch
        {
            LeaderboardOption.Top => (UserId?)null,
            LeaderboardOption.Me => ctx.User.GetUserId(),
            LeaderboardOption.User => user!.GetUserId(),
            _ => throw new InvariantViolationException()
        };

        await (await this.GetLeaderboardAsync(guild.GetGuildId(), targetUserId, CancellationToken.None)).Match(
            r => ctx.ReplyAsync(GrimoireColor.DarkPurple, title: "LeaderBoard",
                message: r.LeaderboardText, footer: $"Total Users {r.TotalUserCount}").AsTask(),
            _ => ctx.ReplyAsync(GrimoireColor.Yellow, "User not found on the leaderboard").AsTask());
    }

    /// <summary>
    ///     Fifteen rows of the guild's leaderboard: the top, or, with <paramref name="centredOn" />, five above that
    ///     member and nine below, shifted up near the bottom so fifteen rows still show.
    /// </summary>
    internal async Task<Result<Response>> GetLeaderboardAsync(GuildId guildId, UserId? centredOn, CancellationToken ct)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
        var board = dbContext.LeaderboardView.AsNoTracking().Where(x => x.GuildId == guildId);
        var totalCount = await board.CountAsync(ct);

        var firstRank = 1L;
        if (centredOn is { } userId)
        {
            if (await board.Where(x => x.UserId == userId).Select(x => (long?)x.Rank).FirstOrDefaultAsync(ct)
                is not { } rank)
                return new Result<Response>.NotFound(
                    new Error("leaderboard.user-not-found", "User not found on the leaderboard"));
            firstRank = Math.Max(1, Math.Min(rank - 5, totalCount - 14));
        }

        var rows = await board
            .Where(x => x.Rank >= firstRank && x.Rank < firstRank + 15)
            .OrderBy(x => x.Rank)
            .ToArrayAsync(ct);

        var leaderboardText = string.Join('\n', rows.Select(m =>
            $"**{m.Rank}** {UserExtensions.Mention(m.UserId)} **XP:** {m.TotalXp}"));
        return Result<Response>.Ok(new Response(leaderboardText, totalCount));
    }

    internal sealed record Response(string LeaderboardText, int TotalUserCount);
}

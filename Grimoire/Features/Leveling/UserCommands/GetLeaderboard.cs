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
        var guild = ctx.Guild!;
        var member = ctx.Member!;

        if (option == LeaderboardOption.User && user is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "You must specify a user when selecting the 'User' option.");
            return;
        }

        var userCommandChannel = await this._settingsModule.GetUserCommandChannel(guild.GetGuildId());

        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(
                !member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                && userCommandChannel.GetOrElse(() => null) != ctx.GetChannelId());
        else if (!member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                 && userCommandChannel.GetOrElse(() => null) != ctx.GetChannelId())
            return;

        var targetUserId = option switch
        {
            LeaderboardOption.Top => (UserId?)null,
            LeaderboardOption.Me => ctx.User.GetUserId(),
            LeaderboardOption.User => user!.GetUserId(),
            _ => throw new UnreachableException()
        };

        var response = targetUserId is null
            ? await this.GetTopLeaderboardAsync(guild.GetGuildId(), CancellationToken.None)
            : await this.GetUserCenteredLeaderboardAsync(guild.GetGuildId(), targetUserId.Value,
                CancellationToken.None);

        await response.Match(
            r => ctx.ReplyAsync(GrimoireColor.DarkPurple, title: "LeaderBoard",
                message: r.LeaderboardText, footer: $"Total Users {r.TotalUserCount}").AsTask(),
            _ => ctx.ReplyAsync(GrimoireColor.Yellow, "User not found on the leaderboard").AsTask());
    }

    internal async Task<Result<Response>> GetTopLeaderboardAsync(GuildId guildId, CancellationToken ct)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);

        var rankedMembers = await dbContext.LeaderboardView
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Rank)
            .Take(15)
            .ToArrayAsync(ct);

        var totalMemberCount = await dbContext.LeaderboardView
            .Where(x => x.GuildId == guildId)
            .CountAsync(ct);

        var leaderboardText = string.Join('\n', rankedMembers.Select(m =>
            $"**{m.Rank}** {UserExtensions.Mention(m.UserId)} **XP:** {m.TotalXp}"));

        return Result<Response>.Ok(new Response(leaderboardText, totalMemberCount));
    }

    internal async Task<Result<Response>> GetUserCenteredLeaderboardAsync(
        GuildId guildId, UserId userId, CancellationToken ct)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);

        var userEntry = await dbContext.Set<LeaderboardView>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId, ct);

        if (userEntry is null)
            return new Result<Response>.NotFound(
                new Error("leaderboard.user-not-found", "User not found on the leaderboard"));

        var surroundingUsers = await dbContext.Set<LeaderboardView>()
            .AsNoTracking()
            .Where(x => x.GuildId == guildId &&
                        x.Rank >= userEntry.Rank - 5 &&
                        x.Rank <= userEntry.Rank + 9)
            .OrderBy(x => x.Rank)
            .ToArrayAsync(ct);

        var totalCount = await dbContext.Set<LeaderboardView>()
            .Where(x => x.GuildId == guildId)
            .CountAsync(ct);

        var leaderboardText = string.Join('\n', surroundingUsers.Select(m =>
            $"**{m.Rank}** {UserExtensions.Mention(m.UserId)} **XP:** {m.TotalXp}"));

        return Result<Response>.Ok(new Response(leaderboardText, totalCount));
    }

    internal sealed record Response(string LeaderboardText, int TotalUserCount);
}

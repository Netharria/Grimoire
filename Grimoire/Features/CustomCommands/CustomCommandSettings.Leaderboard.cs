// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    [UsedImplicitly]
    [RequireGuild]
    [RequireModuleEnabled(Module.Commands)]
    [Command("Leaderboard")]
    [Description("View command usage leaderboard. Omit name for overall rankings.")]
    public async Task Leaderboard(
        CommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions>]
        [Parameter("name")]
        [Description("A specific command to see its per-user leaderboard. Leave blank for overall.")]
        CustomCommandName? name = null)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.RequiredGuild;

        var guildId = guild.GetGuildId();
        var member = ctx.RequiredMember;
        await (name is null
                ? this.GetOverallLeaderboardAsync(guildId, member)
                : this.GetCommandLeaderboardAsync(guildId, member, name.Value))
            .Match(
                display => ctx.ReplyAsync(GrimoireColor.Purple, display.Text, display.Title, display.Footer).AsTask(),
                error => ctx.SendWarningResponseAsync(error.Message).AsTask());
    }

    private async Task<Result<LeaderboardDisplay>> GetOverallLeaderboardAsync(GuildId guildId, DiscordMember member)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        // Commands the member can't run are neither ranked nor counted (KI-86).
        var hiddenNames = HiddenNames(dbContext, guildId, member);
        var usages = dbContext.CustomCommandUsages.Where(x => x.GuildId == guildId && !hiddenNames.Contains(x.Name));
        var rankings = await usages
            .GroupBy(x => x.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToListAsync();
        // The footer counts everything, not only the fifteen shown.
        var (commandCount, useCount) = (await usages.Select(x => x.Name).Distinct().CountAsync(), await usages.CountAsync());
        return rankings.Count == 0
            ? Result<LeaderboardDisplay>.Fail(new Error("command.leaderboard.empty", "No commands have been used yet."))
            : Result<LeaderboardDisplay>.Ok(new LeaderboardDisplay(
                string.Join("\n", rankings.Select((r, i) => $"**{i + 1}.** `!{r.Name}` — {r.Count} uses")),
                "Command Leaderboard",
                $"{commandCount} commands, {useCount} uses"));
    }

    private async Task<Result<LeaderboardDisplay>> GetCommandLeaderboardAsync(GuildId guildId, DiscordMember member,
        CustomCommandName name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        // A command the member can't run is reported as missing, so its name isn't given away (KI-86).
        if (await HiddenNames(dbContext, guildId, member).ContainsAsync(name))
            return Result<LeaderboardDisplay>.Fail(CommandNotFound(name));
        var rankings = await dbContext.CustomCommandUsages
            .Where(x => x.GuildId == guildId && x.Name == name)
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToListAsync();
        if (rankings.Count == 0)
            return Result<LeaderboardDisplay>.Fail(
                await dbContext.CustomCommands.AnyAsync(x => x.GuildId == guildId && x.Name == name)
                    ? new Error("command.leaderboard.command_empty", $"Command `!{name}` has not been used yet.")
                    : CommandNotFound(name));

        // The footer counts every use, not only those of the fifteen shown.
        var useCount = await dbContext.CustomCommandUsages.CountAsync(x => x.GuildId == guildId && x.Name == name);
        return Result<LeaderboardDisplay>.Ok(new LeaderboardDisplay(
            string.Join("\n",
                rankings.Select((r, i) => $"**{i + 1}.** {UserExtensions.Mention(r.UserId)} — {r.Count} uses")),
            $"Leaderboard for !{name}",
            $"{useCount} total uses"));
    }

    private static IQueryable<CustomCommandName> HiddenNames(GrimoireDbContext dbContext, GuildId guildId,
        DiscordMember member)
        => dbContext.CustomCommands.LatestVersions().Where(x => x.GuildId == guildId).HiddenFrom(member)
            .Select(x => x.Name);

    private static Error CommandNotFound(CustomCommandName name)
        => new("command.leaderboard.not_found", $"No command named `{name}` exists.");

    private sealed record LeaderboardDisplay(string Text, string Title, string Footer);
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
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
        [Parameter("Name")]
        [Description("A specific command to see its per-user leaderboard. Leave blank for overall.")]
        CustomCommandName? name = null)
    {
        await ctx.DeferResponseAsync();
        if (ctx.GetRequiredGuild() is not Validation<DiscordGuild>.Valid { Value: var guild })
        {
            await ctx.SendErrorResponseAsync("This command can only be used in a server.");
            return;
        }

        var guildId = guild.GetGuildId();
        await (name is null
                ? GetOverallLeaderboardAsync(guildId)
                : GetCommandLeaderboardAsync(guildId, name.Value))
            .Match(
                display => ctx.ReplyAsync(GrimoireColor.Purple, display.Text, display.Title, display.Footer ?? "")
                    .AsTask(),
                error => ctx.SendWarningResponseAsync(error.Message).AsTask());
    }

    private async Task<Result<LeaderboardDisplay>> GetOverallLeaderboardAsync(GuildId guildId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var rankings = await dbContext.CustomCommandUsages
            .Where(x => x.GuildId == guildId)
            .GroupBy(x => x.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToListAsync();
        return rankings.Count == 0
            ? Result<LeaderboardDisplay>.Fail(new Error("command.leaderboard.empty", "No commands have been used yet."))
            : Result<LeaderboardDisplay>.Ok(new LeaderboardDisplay(
                string.Join("\n", rankings.Select((r, i) => $"**{i + 1}.** `!{r.Name}` — {r.Count} uses")),
                "Command Leaderboard",
                $"{rankings.Count} commands"));
    }

    private async Task<Result<LeaderboardDisplay>> GetCommandLeaderboardAsync(GuildId guildId, CustomCommandName name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var rankings = await dbContext.CustomCommandUsages
            .Where(x => x.GuildId == guildId && x.Name == name)
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToListAsync();
        return rankings.Count == 0
            ? Result<LeaderboardDisplay>.Fail(new Error("command.leaderboard.command_empty",
                $"Command `!{name}` has not been used yet."))
            : Result<LeaderboardDisplay>.Ok(new LeaderboardDisplay(
                string.Join("\n",
                    rankings.Select((r, i) => $"**{i + 1}.** {UserExtensions.Mention(r.UserId)} — {r.Count} uses")),
                $"Leaderboard for !{name}",
                $"{rankings.Sum(r => r.Count)} total uses"));
    }

    private sealed record LeaderboardDisplay(string Text, string Title, string? Footer);
}

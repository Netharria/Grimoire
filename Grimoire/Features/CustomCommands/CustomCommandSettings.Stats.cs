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
    [Command("Stats")]
    [Description("View usage statistics for a command.")]
    public async Task Stats(
        CommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions>]
        [Parameter("Name")]
        [Description("The name of the command to view stats for.")]
        CustomCommandName name)
    {
        await ctx.DeferResponseAsync();
        var (usage, topUsers) = await QueryStatsAsync(ctx.Guild!.GetGuildId(), name);
        await ctx.ReplyAsync(embed: BuildStatsEmbed(name, usage, topUsers));
    }

    private async Task<(UsageStats? Usage, IReadOnlyList<TopUser> TopUsers)> QueryStatsAsync(GuildId guildId, CustomCommandName name)
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var raw = await dbContext.CustomCommandUsages
            .Where(x => x.GuildId == guildId && x.Name == name)
            .GroupBy(_ => true)
            .Select(g => new
            {
                Total = g.Count(),
                LastMonth = g.Count(x => x.UsedAt > now.AddDays(-30)),
                LastYear = g.Count(x => x.UsedAt > now.AddDays(-365)),
                LastUsed = g.Max(x => (DateTimeOffset?)x.UsedAt)
            })
            .FirstOrDefaultAsync();

        var usage = raw is null ? null : new UsageStats(raw.Total, raw.LastMonth, raw.LastYear, raw.LastUsed);

        var topUsersRaw = await dbContext.CustomCommandUsages
            .Where(x => x.GuildId == guildId && x.Name == name)
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(3)
            .ToListAsync();

        var topUsers = topUsersRaw.Select(u => new TopUser(u.UserId, u.Count)).ToList();
        return (usage, topUsers);
    }

    private static DiscordEmbed BuildStatsEmbed(CustomCommandName name, UsageStats? usage, IReadOnlyList<TopUser> topUsers)
    {
        var topUsersText = topUsers.Count > 0
            ? string.Join("\n", topUsers.Select((u, i) => $"**{i + 1}.** {UserExtensions.Mention(u.UserId)} — {u.Count} uses"))
            : "No usage data yet.";

        return new DiscordEmbedBuilder()
            .WithAuthor($"Stats for !{name}")
            .WithColor(GrimoireColor.Purple)
            .AddField("Total Uses", (usage?.Total ?? 0).ToString(), true)
            .AddField("Last 30 Days", (usage?.LastMonth ?? 0).ToString(), true)
            .AddField("Last Year", (usage?.LastYear ?? 0).ToString(), true)
            .AddField("Last Used",
                usage?.LastUsed is { } lastUsed ? $"<t:{lastUsed.ToUnixTimeSeconds()}:R>" : "Never",
                true)
            .AddField("Top Users", topUsersText)
            .Build();
    }

    private sealed record UsageStats(int Total, int LastMonth, int LastYear, DateTimeOffset? LastUsed);
    private sealed record TopUser(UserId UserId, int Count);
}

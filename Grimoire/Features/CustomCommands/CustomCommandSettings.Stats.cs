// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data.Common;
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
        await QueryStatsAsync(ctx.Guild!.GetGuildId(), name)
            .Match(
            r => ctx.ReplyAsync(embed: BuildStatsEmbed(name, r.Usage, r.TopUsers)).AsTask(),
            error => ctx.SendErrorResponseAsync(error.Message).AsTask());
    }

    private async Task<Result<StatsQueryResult>> QueryStatsAsync(GuildId guildId, CustomCommandName name)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var now = DateTimeOffset.UtcNow;
            var usage = await FetchUsageStatsAsync(dbContext, guildId, name, now);
            var topUsers = await FetchTopUsersAsync(dbContext, guildId, name);
            return Result<StatsQueryResult>.Ok(new StatsQueryResult(usage, topUsers));
        }
        catch (DbException)
        {
            return Result<StatsQueryResult>.Fail(
                new Error("command.stats.db_error",
                    "Could not retrieve stats right now. Please try again."));
        }
    }

    private static async Task<UsageStats?> FetchUsageStatsAsync(GrimoireDbContext dbContext, GuildId guildId,
        CustomCommandName name, DateTimeOffset now)
    {
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
        return raw is null ? null : new UsageStats(raw.Total, raw.LastMonth, raw.LastYear, raw.LastUsed);
    }

    private static async Task<IReadOnlyList<TopUser>> FetchTopUsersAsync(GrimoireDbContext dbContext, GuildId guildId,
        CustomCommandName name)
    {
        var raw = await dbContext.CustomCommandUsages
            .Where(x => x.GuildId == guildId && x.Name == name)
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(3)
            .ToListAsync();
        return raw.Select(u => new TopUser(u.UserId, u.Count)).ToList();
    }

    private static DiscordEmbed BuildStatsEmbed(CustomCommandName name, UsageStats? usage,
        IReadOnlyList<TopUser> topUsers)
    {
        var topUsersText = topUsers.Count > 0
            ? string.Join("\n",
                topUsers.Select((u, i) => $"**{i + 1}.** {UserExtensions.Mention(u.UserId)} — {u.Count} uses"))
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

    private sealed record StatsQueryResult(UsageStats? Usage, IReadOnlyList<TopUser> TopUsers);

    private sealed record UsageStats(int Total, int LastMonth, int LastYear, DateTimeOffset? LastUsed);

    private sealed record TopUser(UserId UserId, int Count);
}

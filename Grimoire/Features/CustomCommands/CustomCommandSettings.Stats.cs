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
    [Command("Stats")]
    [Description("View usage statistics for a command.")]
    public async Task Stats(
        CommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions>]
        [Parameter("name")]
        [Description("The name of the command to view stats for.")]
        CustomCommandName name)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.RequiredGuild;

        await this.QueryStatsAsync(guild.GetGuildId(), ctx.RequiredMember, name)
            .Match(
                r => ctx.ReplyAsync(embed: BuildStatsEmbed(name, r.Usage, r.TopUsers)).AsTask(),
                error => ctx.SendErrorResponseAsync(error.Message).AsTask(),
                onNotFound: error => ctx.SendWarningResponseAsync(error.Message).AsTask());
    }

    private async Task<Result<StatsQueryResult>> QueryStatsAsync(GuildId guildId, DiscordMember member,
        CustomCommandName name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        // As /Commands History says: zeros would read as a command nobody uses. A command the member can't run is
        // reported the same way, so its name isn't given away (KI-86).
        if (!await dbContext.CustomCommands.LatestVersions().VisibleTo(member)
                .AnyAsync(x => x.GuildId == guildId && x.Name == name))
            return new Result<StatsQueryResult>.NotFound(
                new Error("command.stats.not_found", $"No command named `{name}` exists."));
        var now = DateTimeOffset.UtcNow;
        var usage = await FetchUsageStatsAsync(dbContext, guildId, name, now);
        var topUsers = await FetchTopUsersAsync(dbContext, guildId, name);
        return Result<StatsQueryResult>.Ok(new StatsQueryResult(usage, topUsers));
    }

    private static Task<UsageStats?> FetchUsageStatsAsync(GrimoireDbContext dbContext, GuildId guildId,
        CustomCommandName name, DateTimeOffset now)
        => dbContext.CustomCommandUsages
            .Where(x => x.GuildId == guildId && x.Name == name)
            .GroupBy(_ => true)
            .Select(g => new UsageStats(
                g.Count(),
                g.Count(x => x.UsedAt > now.AddDays(-30)),
                g.Count(x => x.UsedAt > now.AddDays(-365)),
                g.Max(x => (DateTimeOffset?)x.UsedAt)))
            .FirstOrDefaultAsync();

    private static async Task<IReadOnlyList<TopUser>> FetchTopUsersAsync(GrimoireDbContext dbContext, GuildId guildId,
        CustomCommandName name)
        => await dbContext.CustomCommandUsages
            .Where(x => x.GuildId == guildId && x.Name == name)
            .GroupBy(x => x.UserId)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => new TopUser(g.Key, g.Count()))
            .ToListAsync();

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

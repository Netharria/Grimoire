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

        if (ctx.Guild is not { } guild)
        {
            await ctx.SendWarningResponseAsync("This command can only be used in a server.");
            return;
        }

        var guildId = guild.GetGuildId();
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var stats = await dbContext.CustomCommandUsages
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

        var topUsers = await dbContext.CustomCommandUsages
            .Where(x => x.GuildId == guildId && x.Name == name)
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(3)
            .ToListAsync();

        var topUsersText = topUsers.Count > 0
            ? string.Join("\n", topUsers.Select((u, i) =>
                $"**{i + 1}.** {UserExtensions.Mention(u.UserId)} — {u.Count} uses"))
            : "No usage data yet.";

        await ctx.ReplyAsync(embed: new DiscordEmbedBuilder()
            .WithAuthor($"Stats for !{name}")
            .WithColor(GrimoireColor.Purple)
            .AddField("Total Uses", (stats?.Total ?? 0).ToString(), inline: true)
            .AddField("Last 30 Days", (stats?.LastMonth ?? 0).ToString(), inline: true)
            .AddField("Last Year", (stats?.LastYear ?? 0).ToString(), inline: true)
            .AddField("Last Used",
                stats?.LastUsed is { } lastUsed
                    ? $"<t:{lastUsed.ToUnixTimeSeconds()}:R>"
                    : "Never",
                inline: true)
            .AddField("Top Users", topUsersText));
    }
}

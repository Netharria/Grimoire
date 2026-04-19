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

        if (ctx.Guild is not { } guild)
        {
            await ctx.SendWarningResponseAsync("This command can only be used in a server.");
            return;
        }

        var guildId = guild.GetGuildId();
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        if (name is null)
        {
            var rankings = await dbContext.CustomCommandUsages
                .Where(x => x.GuildId == guildId)
                .GroupBy(x => x.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(15)
                .ToListAsync();

            if (rankings.Count == 0)
            {
                await ctx.ReplyAsync(GrimoireColor.Purple, "No commands have been used yet.");
                return;
            }

            var text = string.Join("\n", rankings.Select((r, i) =>
                $"**{i + 1}.** `!{r.Name}` — {r.Count} uses"));

            await ctx.ReplyAsync(GrimoireColor.Purple, text, title: "Command Leaderboard",
                footer: $"{rankings.Count} commands");
        }
        else
        {
            var rankings = await dbContext.CustomCommandUsages
                .Where(x => x.GuildId == guildId && x.Name == name)
                .GroupBy(x => x.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(15)
                .ToListAsync();

            if (rankings.Count == 0)
            {
                await ctx.ReplyAsync(GrimoireColor.Purple, $"Command `!{name}` has not been used yet.");
                return;
            }

            var text = string.Join("\n", rankings.Select((r, i) =>
                $"**{i + 1}.** {UserExtensions.Mention(r.UserId)} — {r.Count} uses"));

            await ctx.ReplyAsync(GrimoireColor.Purple, text, title: $"Leaderboard for !{name}",
                footer: $"{rankings.Sum(r => r.Count)} total uses");
        }
    }
}

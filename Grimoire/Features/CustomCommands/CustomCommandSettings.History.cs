// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
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
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("History")]
    [Description("View the version history of a command.")]
    public async Task History(
        CommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions>]
        [Parameter("Name")]
        [Description("The name of the command to view history for.")]
        CustomCommandName name)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is not { } guild)
        {
            await ctx.SendWarningResponseAsync("This command can only be used in a server.");
            return;
        }

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var versions = await dbContext.CustomCommands
            .AsNoTracking()
            .Where(x => x.GuildId == guild.GetGuildId() && x.Name == name)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.CreatedAt, x.ModeratorId, x.Content })
            .ToListAsync();

        if (versions.Count == 0)
        {
            await ctx.SendWarningResponseAsync($"No command named `{name}` exists.");
            return;
        }

        var stringBuilder = new StringBuilder();
        var pages = new List<string>();

        for (var i = 0; i < versions.Count; i++)
        {
            var v = versions[i];
            var preview = v.Content.Length > 100 ? string.Concat(v.Content.AsSpan(0, 100), "…") : v.Content;
            var entry =
                $"**v{i + 1}{(i == 0 ? " (current)" : string.Empty)}** — <t:{v.CreatedAt.ToUnixTimeSeconds()}:f>"
                + (v.ModeratorId is { } mod ? $" by {UserExtensions.Mention(mod)}" : string.Empty)
                + $"\n> {preview}\n";

            if (stringBuilder.Length + entry.Length > 2048)
            {
                pages.Add(stringBuilder.ToString());
                stringBuilder.Clear();
            }

            stringBuilder.Append(entry);
        }

        if (stringBuilder.Length > 0)
            pages.Add(stringBuilder.ToString());

        foreach (var page in pages)
            await ctx.ReplyAsync(GrimoireColor.Purple, page, $"Version history for {name}");
    }
}

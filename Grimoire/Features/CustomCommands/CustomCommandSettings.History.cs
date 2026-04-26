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
        await GetVersionsAsync(ctx.Guild!.GetGuildId(), name)
            .Match(
                versions => SendHistoryPagesAsync(ctx, name, versions),
                errors => ctx.SendWarningResponseAsync(errors[0].Message).AsTask());
    }

    private async Task<Result<IReadOnlyList<CommandVersion>>> GetVersionsAsync(GuildId guildId, CustomCommandName name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var versions = await dbContext.CustomCommands
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.Name == name)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CommandVersion(x.CreatedAt, x.ModeratorId, x.Content))
            .ToListAsync();
        return versions.Count == 0
            ? Result<IReadOnlyList<CommandVersion>>.Fail(new Error("command.history.not_found", $"No command named `{name}` exists."))
            : Result<IReadOnlyList<CommandVersion>>.Ok(versions);
    }

    private static async Task SendHistoryPagesAsync(CommandContext ctx, CustomCommandName name, IReadOnlyList<CommandVersion> versions)
    {
        foreach (var page in BuildPages(versions))
            await ctx.ReplyAsync(GrimoireColor.Purple, page, $"Version history for {name}");
    }

    private static IEnumerable<string> BuildPages(IReadOnlyList<CommandVersion> versions)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < versions.Count; i++)
        {
            var entry = FormatEntry(versions[i], i);
            if (builder.Length + entry.Length > 2048)
            {
                yield return builder.ToString();
                builder.Clear();
            }
            builder.Append(entry);
        }
        if (builder.Length > 0)
            yield return builder.ToString();
    }

    private static string FormatEntry(CommandVersion version, int index)
    {
        var preview = version.Content.Length > 100
            ? string.Concat(version.Content.AsSpan(0, 100), "…")
            : version.Content;
        return $"**v{index + 1}{(index == 0 ? " (current)" : string.Empty)}** — <t:{version.CreatedAt.ToUnixTimeSeconds()}:f>"
            + (version.ModeratorId is { } mod ? $" by {UserExtensions.Mention(mod)}" : string.Empty)
            + $"\n> {preview}\n";
    }

    private sealed record CommandVersion(DateTimeOffset CreatedAt, ModeratorId? ModeratorId, string Content);
}

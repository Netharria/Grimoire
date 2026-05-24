// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

[UsedImplicitly]
internal sealed class GetCustomCommandOptions(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IAutoCompleteProvider
{
    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context) =>
        await Validation<AutoCompleteContext>.Succeed(context)
            .Bind(ctx => ctx switch
            {
                { Guild: not null } => Validation<AutoCompleteContext>.Succeed(ctx),
                _ => Validation<AutoCompleteContext>.Fail(new Error(
                    "command-autocomplete.not-in-guild", "This command was not used in a guild"))
            })
            .ToResult()
            .BindAsync(async ctx =>
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync();
                var guildId = ctx.Guild!.GetGuildId();
                var cleanedText = ctx.UserInput?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                var choices = await GetChoicesAsync(dbContext, guildId, cleanedText);
                return Result<IEnumerable<DiscordAutoCompleteChoice>>.Ok(choices);
            })
            .Match(choices => choices, _ => []);

    private static async Task<IEnumerable<DiscordAutoCompleteChoice>> GetChoicesAsync(
        GrimoireDbContext dbContext, GuildId guildId, string? cleanedText)
    {
        // Restrict to the latest version of each command (no newer entry with the same Name + GuildId).
        // Project to raw strings server-side so value-converter wrapper types never appear in
        // the materialised result — the choice label and value are built from plain strings on
        // the client side.
        var latestVersions = dbContext.CustomCommands
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .Where(x => !dbContext.CustomCommands.Any(y =>
                y.GuildId == x.GuildId && y.Name == x.Name && y.CreatedAt > x.CreatedAt));

        var ordered = string.IsNullOrEmpty(cleanedText)
            ? latestVersions.OrderBy(x => x.Name)
            : latestVersions.OrderBy(x =>
                EF.Functions.FuzzyStringMatchLevenshtein(
                    EF.Property<string>(x, "Name").ToLower(), cleanedText.ToLower()));

        // Project to the real CLR types so EF's value converters materialise normally.
        // Accessing .Value happens after ToListAsync, on already-hydrated objects — no
        // shaper coercion between CustomCommandName and string required.
        var commands = await ordered
            .Take(5)
            .Select(x => new { x.Name, x.Content })
            .ToListAsync();

        return commands.Select(x => new DiscordAutoCompleteChoice(
            x.Name.Value
            + (x.Content.Value.Contains("%mention", StringComparison.OrdinalIgnoreCase) ? " <Mention>" : string.Empty)
            + (x.Content.Value.Contains("%message", StringComparison.OrdinalIgnoreCase) ? " <Message>" : string.Empty),
            x.Name.Value));
    }
}

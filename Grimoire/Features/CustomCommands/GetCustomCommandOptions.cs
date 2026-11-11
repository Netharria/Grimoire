// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.DatabaseQueryHelpers;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

[UsedImplicitly]
internal sealed class GetCustomCommandOptions(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IAutoCompleteProvider
{
    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
    {
        if (context is not { Guild: { } guild, Member: { } member })
            return [];
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        // No name is longer than 24, and levenshtein refuses input over 255 characters.
        var cleanedText = context.UserInput?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is { } word
            ? word[..Math.Min(word.Length, 24)]
            : null;
        return await GetChoicesAsync(dbContext, guild.GetGuildId(), member, cleanedText);
    }

    private static async Task<IEnumerable<DiscordAutoCompleteChoice>> GetChoicesAsync(
        GrimoireDbContext dbContext, GuildId guildId, DiscordMember member, string? cleanedText)
    {
        var latestVersions = dbContext.CustomCommands
            .LatestVersions()
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .VisibleTo(member);

        var ordered = string.IsNullOrEmpty(cleanedText)
            ? latestVersions.OrderBy(x => x.Name)
            : latestVersions.OrderBy(x =>
                EF.Functions.FuzzyStringMatchLevenshtein(
                    EF.Property<string>(x, "Name").ToLower(), cleanedText.ToLower()));

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

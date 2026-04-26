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
    private static readonly Func<GrimoireDbContext, GuildId, string, IAsyncEnumerable<DiscordAutoCompleteChoice>>
        _getCommandsAsync =
            EF.CompileAsyncQuery((GrimoireDbContext context, GuildId guildId, string cleanedText) =>
                context.CustomCommands
                    .AsNoTracking()
                    .Where(x => x.GuildId == guildId)
                    .Where(x => !context.CustomCommands.Any(y =>
                        y.GuildId == x.GuildId && y.Name == x.Name && y.CreatedAt > x.CreatedAt))
                    .OrderBy(x =>
                        EF.Functions.FuzzyStringMatchLevenshtein(x.Name.Value.ToLower(), cleanedText.ToLower()))
                    .Take(5)
                    .Select(x => new DiscordAutoCompleteChoice(
                        x.Name
                        + (x.Content.Contains("%mention") ? " <Mention>" : string.Empty)
                        + (x.Content.Contains("%message") ? " <Message>" : string.Empty),
                        x.Name.Value))
            );

    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
    {
        if (context.Guild is null || context.UserInput is null)
            return [];
        var cleanedText = context.UserInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        if (string.IsNullOrEmpty(cleanedText))
            return [];
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        return await _getCommandsAsync(dbContext, new GuildId(context.Guild.Id), cleanedText)
            .ToListAsync();
    }
}

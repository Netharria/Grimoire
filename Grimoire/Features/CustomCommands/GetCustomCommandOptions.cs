// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;

namespace Grimoire.Features.CustomCommands;

public sealed class GetCustomCommandOptions
{
    internal sealed class AutocompleteProvider(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IAutoCompleteProvider
    {
        private static readonly Func<GrimoireDbContext, GuildId, string, IAsyncEnumerable<DiscordAutoCompleteChoice>>
            _getCommandsAsync =
                EF.CompileAsyncQuery((GrimoireDbContext context, GuildId guildId, string cleanedText) =>
                    context.CustomCommands
                        .AsNoTracking()
                        .Where(x => x.GuildId == guildId)
                        .OrderBy(x => EF.Functions.FuzzyStringMatchLevenshtein(x.Name.Value, cleanedText))
                        .Take(5)
                        .Select(x => new DiscordAutoCompleteChoice(
                            x.Name + " " +
                            (x.HasMention ? "<Mention> " : string.Empty) +
                            (x.HasMessage ? "<Message>" : string.Empty),
                            x.Name))
                );

        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
        {
            if (context.Guild is null || string.IsNullOrWhiteSpace(context.UserInput))
                return [];
            var cleanedText = context.UserInput.Split(' ').FirstOrDefault(string.Empty);
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

            return await _getCommandsAsync(dbContext, new GuildId(context.Guild.Id), cleanedText)
                .ToListAsync();
        }
    }
}

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
        s_getCommandsAsync =
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
                        + (x.Content.Value.ToLower().Contains("%mention") ? " <Mention>" : string.Empty)
                        + (x.Content.Value.ToLower().Contains("%message") ? " <Message>" : string.Empty),
                        x.Name.Value))
            );

    private static readonly Func<GrimoireDbContext, GuildId, IAsyncEnumerable<DiscordAutoCompleteChoice>>
        s_getAllCommandsAsync =
            EF.CompileAsyncQuery((GrimoireDbContext context, GuildId guildId) =>
                context.CustomCommands
                    .AsNoTracking()
                    .Where(x => x.GuildId == guildId)
                    .Where(x => !context.CustomCommands.Any(y =>
                        y.GuildId == x.GuildId && y.Name == x.Name && y.CreatedAt > x.CreatedAt))
                    .OrderBy(x => x.Name.Value)
                    .Take(5)
                    .Select(x => new DiscordAutoCompleteChoice(
                        x.Name
                        + (x.Content.Value.ToLower().Contains("%mention") ? " <Mention>" : string.Empty)
                        + (x.Content.Value.ToLower().Contains("%message") ? " <Message>" : string.Empty),
                        x.Name.Value))
            );

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
                var choices = string.IsNullOrEmpty(cleanedText)
                    ? await s_getAllCommandsAsync(dbContext, guildId).ToListAsync()
                    : await s_getCommandsAsync(dbContext, guildId, cleanedText).ToListAsync();
                return Result<IEnumerable<DiscordAutoCompleteChoice>>.Ok(choices);
            })
            .Match(choices => choices, _ => []);
}

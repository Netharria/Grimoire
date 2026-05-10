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
internal sealed class GetCommandVersionOptions(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IAutoCompleteProvider
{
    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context) =>
        await Validation<AutoCompleteContext>.Succeed(context)
            .Bind(ctx =>
                ctx switch
                {
                    { Guild: not null } => Validation<AutoCompleteContext>.Succeed(ctx),
                    _ => Validation<AutoCompleteContext>.Fail(new Error(
                        "command-version-autocomplete.validation.not-in-guild", "This command was not used in a guild"))
                })
            .Map(ctx => ctx.Options
                .FirstOrDefault(o => o.Name.Equals("name", StringComparison.OrdinalIgnoreCase))
                ?.Value as string)
            .Bind(CustomCommandName.Create)
            .ToResult()
            .BindAsync(async name => await Versions(context.Guild!.GetGuildId(), name))
            .Match(
                versions => versions.Select((v, i) =>
                    new DiscordAutoCompleteChoice($"v{i + 1} — {v.Item1:yyyy-MM-dd HH:mm} UTC"
                                                  + (v.Item2 is { } mod
                                                      ? $" by {UserExtensions.Mention(mod)}"
                                                      : string.Empty),
                        v.Item1.ToUnixTimeSeconds().ToString())),
                _ => []);

    private async ValueTask<Result<IEnumerable<(DateTimeOffset, ModeratorId?)>>> Versions(GuildId guildId,
        CustomCommandName name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var versions = await dbContext.CustomCommands
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.Name == name)
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .Select(x => new { x.CreatedAt, x.ModeratorId })
            .ToListAsync();
        return Result<IEnumerable<(DateTimeOffset, ModeratorId?)>>.Ok(
            versions.Select(result => (result.CreatedAt, result.ModeratorId)));
    }
}

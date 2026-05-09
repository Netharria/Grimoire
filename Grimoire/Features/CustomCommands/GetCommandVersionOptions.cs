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
    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
    {
        if (context.Guild is null)
            return [];

        var nameValue = context.Options
            .FirstOrDefault(o => o.Name.Equals("name", StringComparison.OrdinalIgnoreCase))
            ?.Value as string;

        if (string.IsNullOrEmpty(nameValue))
            return [];

        var name = CustomCommandName.ParseFromDatabase(nameValue);
        var guildId = new GuildId(context.Guild.Id);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var versions = await dbContext.CustomCommands
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.Name == name)
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .Select(x => new { x.CreatedAt, x.ModeratorId })
            .ToListAsync();

        return versions.Select((v, i) => new DiscordAutoCompleteChoice(
            $"v{i + 1} — {v.CreatedAt:yyyy-MM-dd HH:mm} UTC"
            + (v.ModeratorId is { } mod ? $" by {UserExtensions.Mention(mod)}" : string.Empty),
            v.CreatedAt.ToUnixTimeSeconds().ToString()));
    }
}

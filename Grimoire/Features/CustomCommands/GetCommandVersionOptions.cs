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
    private const int MaxLabelLength = 100;

    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
        => context.Guild is { } guild
           && context.Options.FirstOrDefault(o => o.Name.Equals("name", StringComparison.OrdinalIgnoreCase))?.Value
               is string rawName
           && CustomCommandName.Create(rawName) is Validation<CustomCommandName>.Valid { Value: var name }
            ? BuildChoices(await this.GetVersionsAsync(guild.GetGuildId(), name), guild)
            : [];

    /// <summary>Numbered from every version, as /Commands History does, though only the newest 25 can be offered.</summary>
    private static IEnumerable<DiscordAutoCompleteChoice> BuildChoices(
        (int Total, IReadOnlyList<CommandVersionEntry> Newest) versions, DiscordGuild? guild)
    {
        var (total, list) = versions;
        return list.Select((v, i) =>
        {
            var versionNumber = total - i;
            var label = $"v{versionNumber} — {v.CreatedAt:MMM d, yyyy h:mm tt} UTC"
                + (v.ModeratorId is { } mod ? $" by {ResolveDisplayName(guild, mod)}" : string.Empty);
            return new DiscordAutoCompleteChoice(
                label.Length > MaxLabelLength ? string.Concat(label.AsSpan(0, MaxLabelLength - 1), "…") : label,
                v.CreatedAt.Ticks.ToString());
        });
    }

    /// <summary>
    /// Returns the member's display name from the guild cache if available,
    /// otherwise falls back to the bare numeric ID.
    /// </summary>
    private static string ResolveDisplayName(DiscordGuild? guild, ModeratorId mod)
        => guild?.Members.TryGetValue(mod.Value, out var member) is true
            ? member.DisplayName
            : mod.Value.ToString();

    private async Task<(int Total, IReadOnlyList<CommandVersionEntry> Newest)> GetVersionsAsync(
        GuildId guildId, CustomCommandName name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var versions = dbContext.CustomCommands.Where(x => x.GuildId == guildId && x.Name == name);
        var total = await versions.CountAsync();
        var newest = await versions
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .Select(x => new CommandVersionEntry(x.CreatedAt, x.ModeratorId))
            .ToListAsync();
        return (total, newest);
    }

    private sealed record CommandVersionEntry(DateTimeOffset CreatedAt, ModeratorId? ModeratorId);
}

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
            .BindAsync(async name => await this.GetVersionsAsync(context.Guild!.GetGuildId(), name))
            .Match(
                versions => BuildChoices(versions, context.Guild),
                _ => []);

    private static IEnumerable<DiscordAutoCompleteChoice> BuildChoices(
        IEnumerable<CommandVersionEntry> versions, DiscordGuild? guild)
    {
        var list = versions.ToList();
        return list.Select((v, i) =>
        {
            var versionNumber = list.Count - i;
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

    private async ValueTask<Result<IEnumerable<CommandVersionEntry>>> GetVersionsAsync(
        GuildId guildId, CustomCommandName name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var versions = await dbContext.CustomCommands
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.Name == name)
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .Select(x => new { x.CreatedAt, x.ModeratorId })
            .ToListAsync();
        return Result<IEnumerable<CommandVersionEntry>>.Ok(
            versions.Select(v => new CommandVersionEntry(v.CreatedAt, v.ModeratorId)));
    }

    private sealed record CommandVersionEntry(DateTimeOffset CreatedAt, ModeratorId? ModeratorId);
}

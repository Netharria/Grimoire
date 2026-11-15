// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public Task<Fin<FrozenSet<DiagnosticsIgnoredItem>>> GetDiagnosticsIgnoredItemsAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
            {
                var items = await this._cache.GetOrCreateAsync(
                    CacheKey.DiagnosticsIgnoredItems(guildId),
                    guildId,
                    async (guildIdState, innerCt) =>
                    {
                        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(innerCt);
                        return (await dbContext.DiagnosticsIgnoredItems
                                .AsNoTracking()
                                .Where(item => item.GuildId == guildIdState)
                                .ToListAsync(innerCt))
                            .ToFrozenSet();
                    },
                    this._cacheEntryOptions,
                    cancellationToken: ct);
                return Fin.Succ<FrozenSet<DiagnosticsIgnoredItem>>(items);
            }, "Could not retrieve the diagnostics ignore list.",
            cancellationToken);

    /// <summary>Adds each item that is not already ignored. Returns only the ones that were added.</summary>
    public Task<Fin<Write<IReadOnlyList<DiagnosticsIgnoredItem>>>> AddDiagnosticsIgnoredItemsAsync(
        GuildId guildId,
        IReadOnlyCollection<DiagnosticsIgnoredItem> items,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync<Write<IReadOnlyList<DiagnosticsIgnoredItem>>>(async ct =>
            {
                if (items.Count == 0)
                    return new Write<IReadOnlyList<DiagnosticsIgnoredItem>>.Unchanged(
                        "No roles or members were provided to ignore.");
                if (items.Any(item => item.GuildId != guildId))
                    return new Invalid(
                        "The guild id provided in the ignored items does not match the expected guild id.");

                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var existing = (await dbContext.DiagnosticsIgnoredItems
                        .AsNoTracking()
                        .Where(item => item.GuildId == guildId)
                        .ToListAsync(ct))
                    .Select(Key)
                    .ToHashSet();
                var toAdd = items.DistinctBy(Key).Where(item => !existing.Contains(Key(item))).ToList();

                if (toAdd.Count == 0)
                    return new Write<IReadOnlyList<DiagnosticsIgnoredItem>>.Unchanged(
                        "Those roles and members are already ignored.");

                dbContext.DiagnosticsIgnoredItems.AddRange(toAdd);
                await dbContext.SaveChangesAsync(ct);
                await this._cache.RemoveAsync(CacheKey.DiagnosticsIgnoredItems(guildId), ct);
                return new Write<IReadOnlyList<DiagnosticsIgnoredItem>>.Saved(toAdd);
            }, "Could not save the diagnostics ignore list.",
            cancellationToken);

    /// <summary>Removes each role or member that is currently ignored. Returns only the ones that were removed.</summary>
    public Task<Fin<IReadOnlyList<DiagnosticsIgnoredItem>>> RemoveDiagnosticsIgnoredItemsAsync(
        GuildId guildId,
        IReadOnlyCollection<DiagnosticsIgnoredItem> items,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var wanted = items.Select(Key).ToHashSet();
                var toRemove = (await dbContext.DiagnosticsIgnoredItems
                        .Where(item => item.GuildId == guildId)
                        .ToListAsync(ct))
                    .Where(item => wanted.Contains(Key(item)))
                    .ToList();

                if (toRemove.Count == 0)
                    return new NotFound("None of those roles or members are ignored.");

                dbContext.DiagnosticsIgnoredItems.RemoveRange(toRemove);
                await dbContext.SaveChangesAsync(ct);
                await this._cache.RemoveAsync(CacheKey.DiagnosticsIgnoredItems(guildId), ct);
                return Fin.Succ<IReadOnlyList<DiagnosticsIgnoredItem>>(toRemove);
            }, "Could not update the diagnostics ignore list.",
            cancellationToken);

    /// <summary>
    ///     Removes a role from the ignore list, for when Discord deletes it: a deleted role can't be picked in
    ///     <c>/Diagnostics Ignore Remove</c>, so it would stay on the list for good (KI-62). NotFound when it wasn't
    ///     ignored.
    /// </summary>
    public Task<Fin<Unit>> RemoveDiagnosticsIgnoredRoleAsync(
        GuildId guildId,
        RoleId roleId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                // Filtered in memory, as in RemoveDiagnosticsIgnoredItemsAsync: EF can't translate RoleId on the
                // derived type.
                var toRemove = (await dbContext.DiagnosticsIgnoredItems
                        .Where(item => item.GuildId == guildId)
                        .ToListAsync(ct))
                    .OfType<DiagnosticsIgnoredRole>()
                    .Where(role => role.RoleId == roleId)
                    .ToList();

                if (toRemove.Count == 0)
                    return new NotFound("That role is not ignored.");

                dbContext.DiagnosticsIgnoredItems.RemoveRange(toRemove);
                await dbContext.SaveChangesAsync(ct);
                await this._cache.RemoveAsync(CacheKey.DiagnosticsIgnoredItems(guildId), ct);
                return Fin.Succ<Unit>(unit);
            }, "Could not update the diagnostics ignore list.",
            cancellationToken);

    // Discord IDs are snowflakes, so a role and a member never share one. The table is keyed by guild and ID.
    private static (string Kind, ulong Id) Key(DiagnosticsIgnoredItem item) => item switch
    {
        DiagnosticsIgnoredRole role => ("Role", role.RoleId.Value),
        DiagnosticsIgnoredMember member => ("Member", member.UserId.Value),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
    };
}

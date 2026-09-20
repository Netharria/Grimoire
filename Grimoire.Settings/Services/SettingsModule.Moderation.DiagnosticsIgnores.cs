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
    public Task<Result<FrozenSet<DiagnosticsIgnoredItem>>> GetDiagnosticsIgnoredItemsAsync(
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
                return Result<FrozenSet<DiagnosticsIgnoredItem>>.Ok(items);
            }, new Error("diagnostics-ignored-items.lookup-failed", "Could not retrieve the diagnostics ignore list."),
            cancellationToken);

    /// <summary>Adds each item that is not already ignored. Returns only the ones that were added.</summary>
    public Task<Result<IReadOnlyList<DiagnosticsIgnoredItem>>> AddDiagnosticsIgnoredItemsAsync(
        GuildId guildId,
        IReadOnlyCollection<DiagnosticsIgnoredItem> items,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
            {
                if (items.Count == 0)
                    return new Result<IReadOnlyList<DiagnosticsIgnoredItem>>.NotModified(
                        new Error("diagnostics-ignored-items.empty", "No roles or members were provided to ignore."));
                if (items.Any(item => item.GuildId != guildId))
                    return Result<IReadOnlyList<DiagnosticsIgnoredItem>>.Fail(
                        new Error("diagnostics-ignored-items.guild-id.mismatch",
                            "The guild id provided in the ignored items does not match the expected guild id."));

                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var existing = (await dbContext.DiagnosticsIgnoredItems
                        .AsNoTracking()
                        .Where(item => item.GuildId == guildId)
                        .ToListAsync(ct))
                    .Select(Key)
                    .ToHashSet();
                var toAdd = items.DistinctBy(Key).Where(item => !existing.Contains(Key(item))).ToList();

                if (toAdd.Count == 0)
                    return new Result<IReadOnlyList<DiagnosticsIgnoredItem>>.NotModified(
                        new Error("diagnostics-ignored-items.already-ignored",
                            "Those roles and members are already ignored."));

                dbContext.DiagnosticsIgnoredItems.AddRange(toAdd);
                await dbContext.SaveChangesAsync(ct);
                await this._cache.RemoveAsync(CacheKey.DiagnosticsIgnoredItems(guildId), ct);
                return Result<IReadOnlyList<DiagnosticsIgnoredItem>>.Ok(toAdd);
            }, new Error("diagnostics-ignored-items.save-failed", "Could not save the diagnostics ignore list."),
            cancellationToken);

    /// <summary>Removes each role or member that is currently ignored. Returns only the ones that were removed.</summary>
    public Task<Result<IReadOnlyList<DiagnosticsIgnoredItem>>> RemoveDiagnosticsIgnoredItemsAsync(
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
                    return new Result<IReadOnlyList<DiagnosticsIgnoredItem>>.NotFound(
                        new Error("diagnostics-ignored-items.not-found", "None of those roles or members are ignored."));

                dbContext.DiagnosticsIgnoredItems.RemoveRange(toRemove);
                await dbContext.SaveChangesAsync(ct);
                await this._cache.RemoveAsync(CacheKey.DiagnosticsIgnoredItems(guildId), ct);
                return Result<IReadOnlyList<DiagnosticsIgnoredItem>>.Ok(toRemove);
            }, new Error("diagnostics-ignored-items.remove-failed", "Could not update the diagnostics ignore list."),
            cancellationToken);

    // Discord IDs are snowflakes, so a role and a member never share one. The table is keyed by guild and ID.
    private static (string Kind, ulong Id) Key(DiagnosticsIgnoredItem item) => item switch
    {
        DiagnosticsIgnoredRole role => ("Role", role.RoleId.Value),
        DiagnosticsIgnoredMember member => ("Member", member.UserId.Value),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
    };
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Rows;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

/// <summary>
///     The state changes the three state tables share (TD-1, spec 2.2, section 2.4). Each is an UPDATE filtered on
///     the states it may move from, so it only moves forward, and a race with the expiry poll or another moderator
///     updates no row instead of throwing.
/// </summary>
public sealed partial class SettingsModule
{
    private static readonly string[] _inForceStates = [nameof(ItemState.Pending), nameof(ItemState.Active)];

    private static Expression<Func<T, bool>> InForce<T>() where T : ItemRow
        => row => _inForceStates.Contains(row.State);

    /// <summary>Ends whichever rows of <paramref name="rows" /> are still in force. Returns how many it ended.</summary>
    private static Task<int> EndInForce<T>(IQueryable<T> rows, ItemState ended, CancellationToken cancellationToken)
        where T : ItemRow
        => ended switch
        {
            ItemState.Expired(var at) => rows.Where(InForce<T>()).ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.State, nameof(ItemState.Expired))
                .SetProperty(row => row.EndedAt, at), cancellationToken),
            ItemState.Removed(var at, var by, var reason) => rows.Where(InForce<T>()).ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(row => row.State, nameof(ItemState.Removed))
                    .SetProperty(row => row.EndedAt, at)
                    .SetProperty(row => row.RemovedBy, by)
                    .SetProperty(row => row.RemoveReason, reason), cancellationToken),
            _ => throw new InvariantViolationException("Only Expired and Removed end an item.")
        };

    /// <summary>
    ///     Runs <paramref name="operation" /> in one transaction, inside the retrying execution strategy, which refuses
    ///     a transaction it doesn't own.
    /// </summary>
    private static Task<T> InTransaction<T>(SettingsDbContext dbContext,
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        => dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            var result = await operation(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
}

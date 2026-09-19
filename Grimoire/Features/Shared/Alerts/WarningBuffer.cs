// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Alerts;

/// <summary>Aggregates warnings in memory between database flushes.</summary>
public sealed class WarningBuffer(TimeProvider timeProvider)
{
    public const int MaxKeys = 200;

    private readonly Lock _lock = new();
    private Dictionary<string, PendingWarning> _pending = [];

    /// <returns><c>false</c> if the warning was dropped because the buffer holds too many distinct keys.</returns>
    public bool Add(Alert alert)
    {
        var key = alert.Key;
        var incoming = PendingWarning.From(alert, key, timeProvider.GetUtcNow());

        lock (this._lock)
        {
            if (this._pending.TryGetValue(key, out var existing))
            {
                this._pending[key] = existing.Merge(incoming);
                return true;
            }

            if (this._pending.Count >= MaxKeys)
                return false;

            this._pending[key] = incoming;
            return true;
        }
    }

    public IReadOnlyCollection<PendingWarning> Drain()
    {
        lock (this._lock)
        {
            var drained = this._pending.Values.ToList();
            this._pending = [];
            return drained;
        }
    }
}

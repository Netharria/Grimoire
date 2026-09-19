// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Alerts;

/// <summary>Suppresses repeats of the same alert key within a window. Not thread-safe.</summary>
public sealed class AlertCooldown(TimeSpan window, TimeProvider timeProvider)
{
    private const int PruneThreshold = 1000;

    private readonly Dictionary<string, (DateTimeOffset LastSent, int Suppressed)> _entries = [];

    /// <returns>
    ///     <c>null</c> if the alert is suppressed; otherwise how many repeats were suppressed since it last went out.
    /// </returns>
    public int? TryAcquire(string key)
    {
        var now = timeProvider.GetUtcNow();

        if (this._entries.TryGetValue(key, out var entry))
        {
            if (now - entry.LastSent < window)
            {
                this._entries[key] = entry with { Suppressed = entry.Suppressed + 1 };
                return null;
            }

            this._entries[key] = (now, 0);
            return entry.Suppressed;
        }

        if (this._entries.Count >= PruneThreshold)
            foreach (var expired in this._entries.Where(e => now - e.Value.LastSent >= window).Select(e => e.Key).ToList())
                this._entries.Remove(expired);

        this._entries[key] = (now, 0);
        return 0;
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Utilities;

/// <summary>
///     Spaces out retries of an item that keeps failing, so a permanent problem (for example missing permissions) is
///     not retried every few seconds. Not thread-safe; each background task uses its own.
/// </summary>
public sealed class RetryBackoff(TimeProvider timeProvider)
{
    private const int PruneThreshold = 1000;

    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(15);

    private readonly Dictionary<string, (int Failures, DateTimeOffset NextAttempt)> _entries = [];

    public bool ShouldAttempt(string key)
        => !this._entries.TryGetValue(key, out var entry) || timeProvider.GetUtcNow() >= entry.NextAttempt;

    public void RecordFailure(string key)
    {
        var now = timeProvider.GetUtcNow();
        var failures = this._entries.TryGetValue(key, out var entry) ? entry.Failures + 1 : 1;

        // 30s, 1m, 2m, 4m ... up to the maximum. The exponent is capped so it cannot overflow.
        var delay = TimeSpan.FromTicks(Math.Min(
            FirstDelay.Ticks * (1L << Math.Min(failures - 1, 20)),
            MaxDelay.Ticks));

        if (this._entries.Count >= PruneThreshold)
            foreach (var stale in this._entries.Where(e => e.Value.NextAttempt < now - MaxDelay).Select(e => e.Key)
                         .ToList())
                this._entries.Remove(stale);

        this._entries[key] = (failures, now + delay);
    }

    public void RecordSuccess(string key) => this._entries.Remove(key);
}

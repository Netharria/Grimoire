// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Functional.Boundary;
using LanguageExt;

namespace Grimoire.Functional.Workers;

// A worker's in-memory state, as immutable values with pure transitions. EffectBackgroundService holds one
// WorkerState in an Atom (plan C6). The rules are GenericBackgroundService's and RetryBackoff's, unchanged.

/// <summary>How often one item has failed in a row, and when it may next be tried.</summary>
public sealed record ItemBackoff(int Failures, DateTimeOffset NextAttempt);

/// <summary>Spaces out retries of an item that keeps failing, so a permanent problem isn't retried every tick.</summary>
public sealed record Backoffs(HashMap<string, ItemBackoff> Entries)
{
    public const int PruneThreshold = 1000;
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(15);

    public static Backoffs Empty { get; } = new(HashMap<string, ItemBackoff>());

    [Pure]
    public bool ShouldAttempt(string key, DateTimeOffset now) =>
        this.Entries.Find(key).Match(entry => now >= entry.NextAttempt, () => true);

    /// <summary>30 s, 1 min, 2 min, 4 min … up to 15 min. Idle entries are pruned once there are 1,000.</summary>
    [Pure]
    public Backoffs Failed(string key, DateTimeOffset now)
    {
        var failures = this.Entries.Find(key).Match(entry => entry.Failures + 1, () => 1);
        // The exponent is capped so it can't overflow.
        var delay = TimeSpan.FromTicks(Math.Min(
            FirstDelay.Ticks * (1L << Math.Min(failures - 1, 20)),
            MaxDelay.Ticks));

        var kept = this.Entries.Count >= PruneThreshold
            ? this.Entries.Filter(entry => entry.NextAttempt >= now - MaxDelay)
            : this.Entries;
        return new Backoffs(kept.AddOrUpdate(key, new ItemBackoff(failures, now + delay)));
    }

    [Pure]
    public Backoffs Succeeded(string key) => new(this.Entries.Remove(key));
}

/// <summary>What the five-minute summary line reports.</summary>
public sealed record RunSummary(long Runs, long Failures, TimeSpan Average, TimeSpan Max);

/// <summary>Tick counts since the last summary.</summary>
public sealed record RunStats(long Runs, long Failures, TimeSpan Total, TimeSpan Max, Option<DateTimeOffset> NextSummary)
{
    public static readonly TimeSpan SummaryInterval = TimeSpan.FromMinutes(5);

    public static RunStats Empty { get; } = new(0, 0, TimeSpan.Zero, TimeSpan.Zero, None);

    /// <summary>
    ///     Counts one tick. When a summary is due, returns it and starts counting again. The first tick only starts
    ///     the five-minute clock.
    /// </summary>
    [Pure]
    public (RunStats Stats, Option<RunSummary> Summary) Record(TimeSpan elapsed, bool failed, DateTimeOffset now)
    {
        var counted = this with
        {
            Runs = this.Runs + 1,
            Failures = this.Failures + (failed ? 1 : 0),
            Total = this.Total + elapsed,
            Max = elapsed > this.Max ? elapsed : this.Max,
            NextSummary = this.NextSummary.IfNone(now + SummaryInterval)
        };

        return this.NextSummary.Match(
            due => now >= due
                ? (Empty with { NextSummary = now + SummaryInterval },
                    Some(new RunSummary(counted.Runs, counted.Failures, counted.Total / counted.Runs, counted.Max)))
                : (counted, Option<RunSummary>.None),
            () => (counted, Option<RunSummary>.None));
    }
}

public sealed record WorkerState(int ConsecutiveFailures, RunStats Stats, Backoffs Backoff)
{
    public const int UrgentAfterConsecutiveFailures = 3;

    public static WorkerState Initial { get; } = new(0, RunStats.Empty, Backoffs.Empty);

    [Pure]
    public WorkerState TickSucceeded() => this with { ConsecutiveFailures = 0 };

    [Pure]
    public WorkerState TickFailed() => this with { ConsecutiveFailures = this.ConsecutiveFailures + 1 };

    /// <summary>
    ///     A single failed tick is a warning. From the third in a row the task is effectively down, so it's urgent,
    ///     unless the cause is <c>NotFound</c>: a guild or channel is gone, which is expected.
    /// </summary>
    [Pure]
    public static AlertSeverity SeverityFor(int consecutiveFailures, CommandOutcome outcome) =>
        consecutiveFailures >= UrgentAfterConsecutiveFailures && outcome is not CommandOutcome.NotFound
            ? AlertSeverity.Urgent
            : AlertSeverity.Warning;
}

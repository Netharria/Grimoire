// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using LanguageExt;
using Microsoft.Extensions.Hosting;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Workers;

/// <summary>
///     The worker boundary (spec 1.2, section 2.3). A worker's work is one <c>Eff</c> tick, repeated on a
///     <see cref="Schedule" />. A failed tick is reported and absorbed so the next one still runs; a failed item
///     never stops the others and is never dropped (F-9).
/// </summary>
/// <remarks>
///     All state (consecutive failures, item backoff, the summary counts) is one immutable
///     <see cref="WorkerState" /> in an <see cref="Atom{A}" />. Logs go under the worker's own category (F-12).
/// </remarks>
public abstract class EffectBackgroundService : BackgroundService
{
    private readonly GrimoireEnv _env;
    private readonly TimeSpan _interval;
    private readonly Atom<WorkerState> _state = Atom(WorkerState.Initial);

    protected EffectBackgroundService(GrimoireEnv env, TimeSpan interval)
    {
        this._env = env;
        this._interval = interval;
        this.Items = new Items(this._state, this.TaskName, this.Category);
    }

    private string TaskName => this.GetType().Name;
    private string Category => this.GetType().FullName ?? this.TaskName;

    protected Items Items { get; }

    /// <summary>The worker's current state, for tests.</summary>
    internal WorkerState State => this._state.Value;

    /// <summary>One run of the work, built fresh each tick. Run each item through <see cref="Items" />.</summary>
    protected abstract Eff<GrimoireRuntime, Unit> Tick();

    /// <summary>Runs one supervised tick now, as the schedule would. The integration tests drive workers this way.</summary>
    internal async Task RunTickAsync(CancellationToken cancellationToken)
    {
        using var envIO = EnvIO.New(token: cancellationToken);
        await this.Supervised().RunAsync(new GrimoireRuntime(this._env), envIO);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Spreads the workers' first ticks out, so they don't all hit the database at once.
        var delay = TimeSpan.FromTicks(Random.Shared.NextInt64(0, this._interval.Ticks));
        using var envIO = EnvIO.New(token: stoppingToken);
        var runtime = new GrimoireRuntime(this._env);
        await Log<GrimoireRuntime>.Information(this.Category,
                "Starting Background task {TaskName} with delay {TimeSpan}", this.TaskName, delay)
            .RunAsync(runtime, envIO);

        try
        {
            await Task.Delay(delay, stoppingToken);
            await repeat(Schedule.spaced(this._interval), this.Supervised()).As().RunAsync(runtime, envIO);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown.
        }
    }

    /// <summary>The tick, with any failure reported and turned into success so the schedule keeps going.</summary>
    private Eff<GrimoireRuntime, Unit> Supervised() =>
        from start in Clock<GrimoireRuntime>.Timestamp
        from result in Boundary<GrimoireRuntime>.Attempt(Eff<GrimoireRuntime, Unit>.Pure(unit).Bind(_ => this.Tick()))
        from elapsed in Clock<GrimoireRuntime>.ElapsedSince(start)
        from shuttingDown in Boundary<GrimoireRuntime>.ShuttingDown
        from _ in result.Match(
            Succ: _ => this.Swap(state => state.TickSucceeded()).Map(_ => unit),
            Fail: error => shuttingDown
                // Shutdown ends the schedule; there's nothing to report.
                ? Eff<GrimoireRuntime, Unit>.Fail(error)
                : this.ReportTickFailure(error))
        from now in Clock<GrimoireRuntime>.UtcNow
        from __ in this.RecordRun(elapsed, result.IsFail, now)
        select unit;

    private Eff<GrimoireRuntime, Unit> ReportTickFailure(Error error) =>
        from state in this.Swap(current => current.TickFailed())
        let outcome = Outcome.Classify(error, false)
        from _ in Log<GrimoireRuntime>.Error(this.Category, Boundary<GrimoireRuntime>.ExceptionOf(error),
            "Exception was thrown when running background task {TaskName} (consecutive failures: "
            + "{ConsecutiveFailures}). Message: ({Message})",
            this.TaskName, state.ConsecutiveFailures, error.Messages)
        from __ in Alerts<GrimoireRuntime>.Send(new Alert
        {
            Severity = WorkerState.SeverityFor(state.ConsecutiveFailures, outcome),
            Type = "BackgroundTaskFailure",
            Discriminator = this.TaskName,
            Message = $"{this.TaskName} failed {state.ConsecutiveFailures} time(s) in a row: {error.Messages}",
            Exception = Boundary<GrimoireRuntime>.ExceptionOf(error).OrNull()
        })
        select unit;

    private Eff<GrimoireRuntime, Unit> RecordRun(TimeSpan elapsed, bool failed, DateTimeOffset now)
    {
        // Ticks run one at a time, so reading then swapping can't interleave with another tick.
        var (stats, summary) = this._state.Value.Stats.Record(elapsed, failed, now);
        return from _ in this.Swap(state => state with { Stats = stats })
               from __ in Effect.ForSome(summary, this.LogSummary)
               select unit;
    }

    private Eff<GrimoireRuntime, Unit> LogSummary(RunSummary summary) =>
        Log<GrimoireRuntime>.Information(this.Category,
            "Background task {TaskName} summary: {Runs} runs, {Failures} failures, avg {AvgMs}ms, max {MaxMs}ms, "
            + "working set {WorkingSetMb}MB, managed heap {HeapMb}MB, gen2 collections {Gen2Collections}",
            this.TaskName, summary.Runs, summary.Failures, (long)summary.Average.TotalMilliseconds,
            (long)summary.Max.TotalMilliseconds, Environment.WorkingSet / (1024 * 1024),
            GC.GetTotalMemory(false) / (1024 * 1024), GC.CollectionCount(2));

    private Eff<GrimoireRuntime, WorkerState> Swap(Func<WorkerState, WorkerState> change) =>
        Eff<GrimoireRuntime, WorkerState>.Lift(() => this._state.Swap(change));
}

/// <summary>Runs a worker's items one by one, each with its own backoff, sharing the worker's state.</summary>
public sealed class Items
{
    private readonly string _category;
    private readonly Atom<WorkerState> _state;
    private readonly string _taskName;

    internal Items(Atom<WorkerState> state, string taskName, string category)
    {
        this._state = state;
        this._taskName = taskName;
        this._category = category;
    }

    /// <summary>
    ///     Runs <paramref name="work" /> for one item unless its backoff says wait. A failure is recorded and reported,
    ///     and the next item still runs, except an outage (<c>Unavailable</c>) or shutdown: those fail the tick, and an
    ///     outage doesn't count against the item (Q38). <paramref name="key" /> must be the same on every tick.
    /// </summary>
    public Eff<GrimoireRuntime, Unit> Item(string key, Eff<GrimoireRuntime, Unit> work) =>
        from now in Clock<GrimoireRuntime>.UtcNow
        from _ in this._state.Value.Backoff.ShouldAttempt(key, now)
            ? this.Attempt(key, work, now)
            : Effect.Done<GrimoireRuntime>()
        select unit;

    private Eff<GrimoireRuntime, Unit> Attempt(string key, Eff<GrimoireRuntime, Unit> work, DateTimeOffset now) =>
        from result in Boundary<GrimoireRuntime>.Attempt(Eff<GrimoireRuntime, Unit>.Pure(unit).Bind(_ => work))
        from shuttingDown in Boundary<GrimoireRuntime>.ShuttingDown
        from _ in result.Match(
            Succ: _ => this.Swap(state => state with { Backoff = state.Backoff.Succeeded(key) }),
            Fail: error => Outcome.Classify(error, shuttingDown) switch
            {
                CommandOutcome.Unavailable or CommandOutcome.Cancelled => Eff<GrimoireRuntime, Unit>.Fail(error),
                var outcome => this.ItemFailed(key, error, outcome, now)
            })
        select unit;

    private Eff<GrimoireRuntime, Unit> ItemFailed(string key, Error error, CommandOutcome outcome,
        DateTimeOffset now) =>
        from _ in this.Swap(state => state with { Backoff = state.Backoff.Failed(key, now) })
        from __ in Log<GrimoireRuntime>.Warning(this._category, Boundary<GrimoireRuntime>.ExceptionOf(error),
            "Background task {TaskName} could not process {Item} ({Outcome}): {Reason}",
            this._taskName, key, outcome, error.Messages)
        // A missing permission is expected, and the work reports it to the guild itself.
        from ___ in outcome is CommandOutcome.Forbidden
            ? Effect.Done<GrimoireRuntime>()
            : Alerts<GrimoireRuntime>.Send(new Alert
            {
                Severity = AlertSeverity.Warning,
                Type = "BackgroundTaskItemFailure",
                Discriminator = this._taskName,
                Message = $"{this._taskName} could not process {key}: {error.Messages}",
                Exception = Boundary<GrimoireRuntime>.ExceptionOf(error).OrNull()
            })
        select unit;

    private Eff<GrimoireRuntime, Unit> Swap(Func<WorkerState, WorkerState> change) =>
        Eff<GrimoireRuntime, Unit>.Lift(() =>
        {
            this._state.Swap(change);
            return unit;
        });
}

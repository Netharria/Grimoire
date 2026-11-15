// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Runtime;
using Grimoire.Functional.Workers;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Unit = LanguageExt.Unit;

namespace Grimoire.UnitTests.Functional;

public sealed class WorkerStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ANewItem_IsAttempted()
        => Backoffs.Empty.ShouldAttempt("a", Now).ShouldBeTrue();

    [Fact]
    public void AFailedItem_WaitsThirtySeconds_ThenDoubles_UpToFifteenMinutes()
    {
        var once = Backoffs.Empty.Failed("a", Now);
        once.ShouldAttempt("a", Now + TimeSpan.FromSeconds(29)).ShouldBeFalse();
        once.ShouldAttempt("a", Now + TimeSpan.FromSeconds(30)).ShouldBeTrue();

        once.Failed("a", Now).Entries["a"].NextAttempt.ShouldBe(Now + TimeSpan.FromMinutes(1));

        var many = Enumerable.Range(0, 40).Aggregate(Backoffs.Empty, (backoff, _) => backoff.Failed("a", Now));
        many.Entries["a"].NextAttempt.ShouldBe(Now + Backoffs.MaxDelay);
        many.Entries["a"].Failures.ShouldBe(40);
    }

    [Fact]
    public void ASucceededItem_StartsAgain()
        => Backoffs.Empty.Failed("a", Now).Succeeded("a").ShouldAttempt("a", Now).ShouldBeTrue();

    [Fact]
    public void OtherItems_AreUnaffected()
        => Backoffs.Empty.Failed("a", Now).ShouldAttempt("b", Now).ShouldBeTrue();

    [Fact]
    public void PastTheThreshold_IdleEntriesArePruned()
    {
        var old = Now - TimeSpan.FromHours(1);
        var full = Enumerable.Range(0, Backoffs.PruneThreshold)
            .Aggregate(Backoffs.Empty, (backoff, index) => backoff.Failed($"old {index}", old));

        var pruned = full.Failed("new", Now);

        pruned.Entries.Keys.ShouldBe(["new"]);
    }

    [Fact]
    public void Pruning_KeepsEntriesStillInsideTheMaximumDelay()
    {
        var full = Enumerable.Range(0, Backoffs.PruneThreshold - 2)
            .Aggregate(Backoffs.Empty, (backoff, index) => backoff.Failed($"old {index}", Now - TimeSpan.FromHours(1)))
            // Due exactly MaxDelay ago: the oldest entry that is kept.
            .Failed("edge", Now - Backoffs.MaxDelay - Backoffs.FirstDelay)
            // Due 30 seconds ago: recent, so kept.
            .Failed("recent", Now - TimeSpan.FromMinutes(1));

        var pruned = full.Failed("new", Now);

        pruned.Entries.Keys.ShouldBe(["edge", "recent", "new"], ignoreOrder: true);
    }

    [Fact]
    public void BelowTheThreshold_NothingIsPruned()
    {
        var old = Now - TimeSpan.FromHours(1);

        Backoffs.Empty.Failed("old", old).Failed("new", Now).Entries.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(1, CommandOutcome.Defect, AlertSeverity.Warning)]
    [InlineData(2, CommandOutcome.Unavailable, AlertSeverity.Warning)]
    [InlineData(3, CommandOutcome.Unavailable, AlertSeverity.Urgent)]
    [InlineData(4, CommandOutcome.Defect, AlertSeverity.Urgent)]
    [InlineData(3, CommandOutcome.NotFound, AlertSeverity.Warning)]
    public void ThreeFailedTicksInARow_AreUrgent_UnlessNotFound(int failures, CommandOutcome outcome,
        AlertSeverity expected)
        => WorkerState.SeverityFor(failures, outcome).ShouldBe(expected);

    [Fact]
    public void ConsecutiveFailures_CountUp_AndResetOnSuccess()
    {
        var failed = WorkerState.Initial.TickFailed().TickFailed();
        failed.ConsecutiveFailures.ShouldBe(2);
        failed.TickSucceeded().ConsecutiveFailures.ShouldBe(0);
    }

    [Fact]
    public void TheFirstTick_StartsTheSummaryClock_WithoutASummary()
    {
        var (stats, summary) = RunStats.Empty.Record(TimeSpan.FromMilliseconds(10), false, Now);

        summary.IsNone.ShouldBeTrue();
        stats.NextSummary.ShouldBe(Some(Now + RunStats.SummaryInterval));
        stats.Runs.ShouldBe(1);
    }

    [Fact]
    public void AfterFiveMinutes_TheSummaryCountsEveryTick_AndStartsAgain()
    {
        var stats = RunStats.Empty;
        (stats, _) = stats.Record(TimeSpan.FromMilliseconds(10), false, Now);
        (stats, _) = stats.Record(TimeSpan.FromMilliseconds(30), true, Now + TimeSpan.FromMinutes(1));
        (stats, var summary) = stats.Record(TimeSpan.FromMilliseconds(20), false, Now + TimeSpan.FromMinutes(5));

        summary.ShouldBe(Some(new RunSummary(3, 1, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(30))));
        stats.ShouldBe(RunStats.Empty with { NextSummary = Now + TimeSpan.FromMinutes(10) });
    }
}

public sealed class EffectBackgroundServiceTests
{
    private readonly TestEnv _env = new();

    [Fact]
    public async Task AFailedItem_DoesNotStopTheNext_AndIsRetriedOnlyAfterItsBackoff()
    {
        var worker = new ProbeWorker(this._env.Env)
        {
            Work = key => key == "bad"
                ? Eff<GrimoireRuntime, Unit>.Fail(new NotFound("channel gone"))
                : Eff<GrimoireRuntime, Unit>.Pure(unit)
        };

        await worker.RunTickAsync(CancellationToken.None);
        worker.Processed.ShouldBe(["bad", "good"]);
        var alert = this._env.Alerts.Sent.ShouldHaveSingleItem();
        alert.Type.ShouldBe("BackgroundTaskItemFailure");
        alert.Severity.ShouldBe(AlertSeverity.Warning);

        await worker.RunTickAsync(CancellationToken.None);
        worker.Processed.ShouldBe(["bad", "good", "good"]);

        this._env.Clock.Advance(Backoffs.FirstDelay);
        await worker.RunTickAsync(CancellationToken.None);
        worker.Processed.ShouldBe(["bad", "good", "good", "bad", "good"]);
        worker.State.ConsecutiveFailures.ShouldBe(0);
    }

    [Fact]
    public async Task AForbiddenItem_IsLogged_ButNotAlerted()
    {
        var worker = new ProbeWorker(this._env.Env)
        {
            Work = _ => Eff<GrimoireRuntime, Unit>.Fail(new Forbidden("missing Manage Channels"))
        };

        await worker.RunTickAsync(CancellationToken.None);

        this._env.Alerts.Sent.ShouldBeEmpty();
        this._env.Log.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning);
        worker.State.Backoff.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ADefectInOneItem_IsBackedOff_AndTheNextStillRuns()
    {
        var worker = new ProbeWorker(this._env.Env)
        {
            Work = key => key == "bad"
                ? Eff<GrimoireRuntime, Unit>.Lift(() => throw new InvalidOperationException("bug"))
                : Eff<GrimoireRuntime, Unit>.Pure(unit)
        };

        await worker.RunTickAsync(CancellationToken.None);

        worker.Processed.ShouldBe(["bad", "good"]);
        worker.State.Backoff.ShouldAttempt("bad", this._env.Clock.Now).ShouldBeFalse();
        this._env.Alerts.Sent.ShouldHaveSingleItem().Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task AnOutage_FailsTheTick_WithoutUsingUpTheItemsBackoff()
    {
        var worker = new ProbeWorker(this._env.Env)
        {
            Work = _ => Eff<GrimoireRuntime, Unit>.Fail(Unavailable.From("db down", new TimeoutException()))
        };

        await worker.RunTickAsync(CancellationToken.None);

        worker.Processed.ShouldBe(["bad"]);
        worker.State.Backoff.Entries.IsEmpty.ShouldBeTrue();
        worker.State.ConsecutiveFailures.ShouldBe(1);
        var alert = this._env.Alerts.Sent.ShouldHaveSingleItem();
        alert.Type.ShouldBe("BackgroundTaskFailure");
        alert.Severity.ShouldBe(AlertSeverity.Warning);
    }

    [Fact]
    public async Task TheThirdFailedTickInARow_IsUrgent_AndASuccessResetsTheCount()
    {
        var down = true;
        var worker = new ProbeWorker(this._env.Env)
        {
            Work = _ => down
                ? Eff<GrimoireRuntime, Unit>.Fail(new Unavailable("db down"))
                : Eff<GrimoireRuntime, Unit>.Pure(unit)
        };

        for (var tick = 0; tick < 3; tick++)
            await worker.RunTickAsync(CancellationToken.None);

        this._env.Alerts.Sent.Map(alert => alert.Severity)
            .ShouldBe([AlertSeverity.Warning, AlertSeverity.Warning, AlertSeverity.Urgent]);

        down = false;
        await worker.RunTickAsync(CancellationToken.None);
        worker.State.ConsecutiveFailures.ShouldBe(0);
    }

    [Fact]
    public async Task EveryTick_IsCounted_AndTheSummaryIsLoggedAfterFiveMinutes()
    {
        var worker = new ProbeWorker(this._env.Env) { Work = _ => Eff<GrimoireRuntime, Unit>.Pure(unit) };

        await worker.RunTickAsync(CancellationToken.None);
        this._env.Clock.Advance(RunStats.SummaryInterval);
        await worker.RunTickAsync(CancellationToken.None);

        var summary = this._env.Log.Entries.ShouldHaveSingleItem();
        summary.Level.ShouldBe(LogLevel.Information);
        summary.Template.ShouldStartWith("Background task {TaskName} summary");
        summary.Category.ShouldBe(typeof(ProbeWorker).FullName);
        summary.Args.Take(3).ShouldBe(["ProbeWorker", 2L, 0L]);
    }

    /// <summary>Processes "bad" then "good" each tick, through <see cref="EffectBackgroundService.Items" />.</summary>
    private sealed class ProbeWorker(GrimoireEnv env) : EffectBackgroundService(env, TimeSpan.FromMinutes(1))
    {
        private readonly Atom<Seq<string>> _processed = Atom(Seq<string>());

        public required Func<string, Eff<GrimoireRuntime, Unit>> Work { get; init; }
        public Seq<string> Processed => this._processed.Value;

        protected override Eff<GrimoireRuntime, Unit> Tick() =>
            from _ in this.Process("bad")
            from __ in this.Process("good")
            select unit;

        private Eff<GrimoireRuntime, Unit> Process(string key) =>
            this.Items.Item(key, Eff<GrimoireRuntime, Unit>.Lift(() =>
            {
                this._processed.Swap(processed => processed.Add(key));
                return unit;
            }).Bind(_ => this.Work(key)));
    }
}

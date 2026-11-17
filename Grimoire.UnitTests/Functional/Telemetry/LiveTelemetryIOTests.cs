// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Data;
using Grimoire.Functional.Telemetry;
using LanguageExt;
using Unit = LanguageExt.Unit;

namespace Grimoire.UnitTests.Functional.Telemetry;

/// <summary>
///     Spec 1.6, section 3.4: the live capability against a real <c>ActivitySource</c> and <c>Meter</c>, each named for
///     this test only, so tests running in parallel never see each other's spans. The nesting tests guard the design's
///     main risk: that <c>Activity.Current</c> flows from a span into everything its body awaits.
/// </summary>
public sealed class LiveTelemetryIOTests : IDisposable
{
    private readonly ConcurrentQueue<Activity> _ended = new();
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<(string Instrument, double Value, Dictionary<string, object?> Tags)> _measured =
        new();

    private readonly MeterListener _meterListener = new();
    private readonly GrimoireMetrics _metrics;
    private readonly string _name = $"Grimoire.Tests.{Guid.NewGuid():N}";
    private readonly ActivitySource _source;
    private readonly ITelemetryIO _telemetry;

    public LiveTelemetryIOTests()
    {
        this._source = new ActivitySource(this._name);
        this._metrics = new GrimoireMetrics(this._name);
        this._listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == this._name,
            // The production rule: drop a parentless span unless it's one of our roots.
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                GrimoireSampler.Decide(options.Parent, options.Tags)
                    ? ActivitySamplingResult.AllDataAndRecorded
                    : ActivitySamplingResult.None,
            ActivityStopped = activity => this._ended.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(this._listener);

        this._meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == this._name)
                listener.EnableMeasurementEvents(instrument);
        };
        this._meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            this.Measured(instrument, value, tags));
        this._meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            this.Measured(instrument, value, tags));
        this._meterListener.Start();

        this._telemetry = new LiveTelemetryIO(this._source, this._metrics, TimeProvider.System);
    }

    public void Dispose()
    {
        this._listener.Dispose();
        this._meterListener.Dispose();
        this._metrics.Dispose();
        this._source.Dispose();
    }

    private static Operation Root => Operation.Event("ProbeHandler", None);
    private static Operation Call => Operation.Database(Sql.Read("Probe.Read", "SELECT 1"));

    private Activity Ended(string name) => this._ended.Single(activity => activity.DisplayName == name);

    private void Measured(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        this._measured.Enqueue((instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));

    private IO<A> Span<A>(Operation operation, IO<A> body) =>
        this._telemetry.Span(operation, body, _ => SpanResult.Succeeded());

    /// <summary>A body that reads the current span after an await, as a library's own instrumentation would.</summary>
    private static IO<string?> CurrentAfterAYield() =>
        IO.liftAsync(async () =>
        {
            await Task.Yield();
            return Activity.Current?.DisplayName;
        });

    [Fact]
    public async Task AChild_NestsUnderItsParent_AcrossAwaits()
    {
        var current = await this.Span(Root, this.Span(Call, CurrentAfterAYield())).RunAsync(EnvIO.New());

        current.ShouldBe("db Probe.Read");
        this.Ended("db Probe.Read").ParentSpanId.ShouldBe(this.Ended("event ProbeHandler").SpanId);
    }

    [Fact]
    public async Task AChild_NestsUnderItsParent_AcrossRetriesTimeoutsAndForks()
    {
        var attempts = 0;
        var flaky = IO.liftAsync(async () =>
        {
            await Task.Yield();
            if (++attempts < 3)
                throw new TimeoutException("slow");
            return Activity.Current?.DisplayName;
        });
        var body = this.Span(Call, flaky.RetryWhile(Schedule.recurs(3), _ => true))
            .Timeout(TimeSpan.FromSeconds(10))
            .Bind(inRetry => CurrentAfterAYield().Fork().Bind(fork => fork.Await).Map(inFork => (inRetry, inFork)));

        var (inRetry, inFork) = await this.Span(Root, body).RunAsync(EnvIO.New());

        inRetry.ShouldBe("db Probe.Read");
        inFork.ShouldBe("event ProbeHandler");
        this.Ended("db Probe.Read").ParentSpanId.ShouldBe(this.Ended("event ProbeHandler").SpanId);
    }

    [Fact]
    public async Task TheSpan_IsNoLongerCurrent_AfterItEnds()
    {
        await this.Span(Root, CurrentAfterAYield()).RunAsync(EnvIO.New());

        Activity.Current.ShouldBeNull();
    }

    [Fact]
    public async Task ARoot_StartsATraceOfItsOwn_WhateverIsCurrent()
    {
        using var ambient = new Activity("ambient").Start();

        await this.Span(Root, CurrentAfterAYield()).RunAsync(EnvIO.New());

        this.Ended("event ProbeHandler").TraceId.ShouldNotBe(ambient.TraceId);
        Activity.Current.ShouldBe(ambient);
    }

    [Fact]
    public async Task TheErrorId_IsTheRootsTraceId()
    {
        var id = await this.Span(Root, this.Span(Call, this._telemetry.ErrorId)).RunAsync(EnvIO.New());

        id.ShouldBe(this.Ended("event ProbeHandler").TraceId.ToHexString());
        id.Length.ShouldBe(32);
    }

    [Fact]
    public async Task OutsideAnySpan_TheErrorId_IsTenRandomHexCharacters()
    {
        var id = await this._telemetry.ErrorId.RunAsync(EnvIO.New());

        id.ShouldMatch("^[0-9a-f]{10}$");
    }

    [Fact]
    public async Task AnOutage_EndsTheSpanAsAnError_AndFailsWithTheSameError()
    {
        var outage = DatabaseErrors.Classify(new TimeoutException(), Sql.Read("Probe.Read", "SELECT 1"), false,
            "db:5432", CancellationToken.None);

        var result = await this.Span(Root, this.Span(Call, IO.fail<int>(outage))).Try().Run().RunAsync(EnvIO.New());

        result.ShouldFail().ShouldBeSameAs(outage);
        var span = this.Ended("db Probe.Read");
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(TelemetryNames.Outcome).ShouldBe("Unavailable");
        span.GetTagItem(TelemetryNames.ErrorType).ShouldBe("Unavailable");
        span.Events.ShouldContain(@event => @event.Name == "exception");
    }

    [Fact]
    public async Task ARefusal_LeavesTheStatusUnset()
    {
        await this.Span(Root, this.Span(Call, IO.fail<int>(new NotFound("gone")))).Try().Run().RunAsync(EnvIO.New());

        var span = this.Ended("db Probe.Read");
        span.Status.ShouldBe(ActivityStatusCode.Unset);
        span.GetTagItem(TelemetryNames.Outcome).ShouldBe("NotFound");
    }

    [Fact]
    public async Task ARoot_CarriesItsTags_AndTheRootMarker()
    {
        await this.Span(Operation.Event("ProbeHandler", new GuildId(10)), IO.pure(unit)).RunAsync(EnvIO.New());

        var span = this.Ended("event ProbeHandler");
        span.Kind.ShouldBe(ActivityKind.Consumer);
        span.GetTagItem(TelemetryNames.GuildId).ShouldBe("10");
        span.GetTagItem(TelemetryNames.Root).ShouldBe(true);
        span.GetTagItem(TelemetryNames.Outcome).ShouldBe("Success");
    }

    [Fact]
    public async Task EventsAndTags_GoOnTheCurrentSpan()
    {
        var body = this._telemetry.Event(TelemetryNames.RetryEvent, Tags.Of((TelemetryNames.Attempt, 2)))
            .Bind(_ => this._telemetry.Tag(Tags.Of((TelemetryNames.WorkerItems, 4))));

        await this.Span(Root, body).RunAsync(EnvIO.New());

        var span = this.Ended("event ProbeHandler");
        span.Events.ShouldContain(@event => @event.Name == TelemetryNames.RetryEvent);
        span.GetTagItem(TelemetryNames.WorkerItems).ShouldBe(4);
    }

    [Fact]
    public async Task ACallsDuration_IsRecorded_EvenWhenItsSpanIsNotSampled()
    {
        // No parent: the sampler drops the span, as for a bridge's call.
        await this.Span(Call, IO.fail<int>(new NotFound("gone"))).Try().Run().RunAsync(EnvIO.New());

        this._ended.ShouldBeEmpty();
        var (_, _, tags) = this._measured.Single(measurement => measurement.Instrument == TelemetryNames.CallDuration);
        tags[TelemetryNames.Dependency].ShouldBe("db");
        tags[TelemetryNames.OperationName].ShouldBe("Probe.Read");
        tags[TelemetryNames.Outcome].ShouldBe("NotFound");
    }

    [Fact]
    public async Task EachKind_RecordsItsOwnMetric()
    {
        await this.Span(Operation.WorkerTick("Probe"),
                this.Span(Operation.WorkerItem("Probe", "a"), IO.pure(unit)))
            .RunAsync(EnvIO.New());
        await this.Span(Root, IO.pure(unit)).RunAsync(EnvIO.New());
        await this.Span(Operation.Command(Invocations.Slash, TimeSpan.Zero), IO.pure(unit)).RunAsync(EnvIO.New());

        var instruments = this._measured.Select(measurement => measurement.Instrument).ToArray();
        instruments.ShouldBe([TelemetryNames.WorkerItemsCounter, TelemetryNames.WorkerTickDuration,
            TelemetryNames.EventDuration], ignoreOrder: true);
    }

    [Fact]
    public async Task ACounter_IsAdded()
    {
        await this._telemetry.Count(Counter.GuildIssues, Tags.Of((TelemetryNames.IssueCode, "BOT-PERM")))
            .RunAsync(EnvIO.New());

        var (_, value, tags) = this._measured.Single(measurement => measurement.Instrument == TelemetryNames.GuildIssues);
        value.ShouldBe(1);
        tags[TelemetryNames.IssueCode].ShouldBe("BOT-PERM");
    }
}

/// <summary>Invocations the telemetry tests share.</summary>
internal static class Invocations
{
    public static Grimoire.Functional.Boundary.Invocation Slash { get; } = new("Probe",
        Grimoire.Functional.Boundary.InvocationKind.Slash, new GuildId(10), new ChannelId(20), new UserId(30),
        DSharpPlus.Entities.DiscordPermissions.None, DateTimeOffset.UnixEpoch);
}

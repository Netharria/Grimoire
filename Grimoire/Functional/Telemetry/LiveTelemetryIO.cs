// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Grimoire.Functional.Capabilities;
using LanguageExt;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Telemetry;

/// <summary>
///     The live telemetry capability (spec 1.6, section 3.4): the only code that starts activities and records
///     measurements for the effects. A span runs its body inside one async method, because <c>Activity.Current</c> is
///     an <c>AsyncLocal</c>: it is current for everything that method awaits, and gone for the caller once it returns.
/// </summary>
internal sealed class LiveTelemetryIO(ActivitySource source, GrimoireMetrics metrics, TimeProvider time)
    : ITelemetryIO
{
    public IO<string> ErrorId => IO.lift(CurrentErrorId);

    public IO<A> Span<A>(Operation operation, IO<A> body, Func<A, SpanEnd> ended) =>
        IO.liftAsync(async env =>
            {
                var started = time.GetTimestamp();
                // A boundary starts a trace of its own, whatever happens to be current.
                if (operation.IsRoot)
                    Activity.Current = null;
                using var activity = source.StartActivity(operation.SpanName, operation.ActivityKind,
                    parentContext: default, tags: ToPairs(operation.StartTags),
                    links: operation.LinkedTo.Map(context => new[] { new ActivityLink(context) }).IfNone([]));

                var result = await body.Try().Run().RunAsync(env);

                var end = SpanResult.Of(result, env.Token.IsCancellationRequested, ended);
                if (activity is not null)
                    Finish(activity, end);
                metrics.Record(operation, end, time.GetElapsedTime(started));
                return result;
            })
            .Bind(result => result.Match(Succ: IO.pure, Fail: IO.fail<A>));

    public IO<Unit> Event(string name, Tags tags) =>
        IO.lift(() =>
        {
            Activity.Current?.AddEvent(new ActivityEvent(name, tags: new ActivityTagsCollection(ToPairs(tags))));
            return unit;
        });

    public IO<Unit> Tag(Tags tags) =>
        IO.lift(() =>
        {
            if (Activity.Current is { } activity)
                foreach (var (name, value) in tags.Items)
                    activity.SetTag(name, value);
            return unit;
        });

    public IO<Unit> Count(Counter counter, Tags tags) =>
        IO.lift(() =>
        {
            metrics.Count(counter, tags);
            return unit;
        });

    /// <summary>The trace id of the current span, or 10 random hex characters outside any span, as before 1.6.</summary>
    internal static string CurrentErrorId() =>
        Activity.Current is { TraceId: var traceId } && traceId != default
            ? traceId.ToHexString()
            : RandomNumberGenerator.GetHexString(10, lowercase: true);

    private static void Finish(Activity activity, SpanEnd end)
    {
        activity.SetTag(TelemetryNames.Outcome, end.Outcome);
        foreach (var (name, value) in end.Tags.Items)
            activity.SetTag(name, value);
        if (!end.IsError)
            return;

        activity.SetStatus(ActivityStatusCode.Error);
        end.ErrorType.IfSome(type => activity.SetTag(TelemetryNames.ErrorType, type));
        end.Exception.IfSome(exception => activity.AddException(exception));
    }

    internal static IEnumerable<KeyValuePair<string, object?>> ToPairs(Tags tags) =>
        tags.Items.Select(tag => new KeyValuePair<string, object?>(tag.Name, tag.Value)).ToArray();
}

/// <summary>
///     Grimoire's instruments on its own <c>Meter</c> (spec 1.6, section 7). Durations are in seconds. Guild, user and
///     channel ids never go on a metric: only bounded tags.
/// </summary>
public sealed class GrimoireMetrics : IDisposable
{
    private readonly Counter<long> _alerts;
    private readonly Histogram<double> _callDuration;
    private readonly Histogram<double> _commandDuration;
    private readonly Histogram<double> _eventDuration;
    private readonly Counter<long> _guildIssues;
    private readonly Counter<long> _guildLogPosts;
    private readonly Counter<long> _guildLogSkipped;
    private readonly Counter<long> _pluralKitQueueFull;
    private readonly Histogram<double> _pluralKitQueueWait;
    private readonly Counter<long> _retries;
    private readonly Histogram<double> _workerTickDuration;
    private readonly Counter<long> _workerItems;

    public GrimoireMetrics(string meterName = TelemetryNames.Source)
    {
        this.Meter = new Meter(meterName);
        var buckets = new InstrumentAdvice<double> { HistogramBucketBoundaries = TelemetryNames.DurationBuckets };
        this._commandDuration = this.Meter.CreateHistogram(TelemetryNames.CommandDuration, "s",
            "A command, from Discord's timestamp to the end of its reply", advice: buckets);
        this._eventDuration = this.Meter.CreateHistogram(TelemetryNames.EventDuration, "s",
            "An event handler run through EventRunner", advice: buckets);
        this._workerTickDuration = this.Meter.CreateHistogram(TelemetryNames.WorkerTickDuration, "s",
            "One tick of a background worker", advice: buckets);
        this._callDuration = this.Meter.CreateHistogram(TelemetryNames.CallDuration, "s",
            "One call to a dependency, retries included", advice: buckets);
        this._pluralKitQueueWait = this.Meter.CreateHistogram(TelemetryNames.PluralKitQueueWait, "s",
            "How long a PluralKit lookup waited for the rate limiter", advice: buckets);
        this._retries = this.Meter.CreateCounter<long>(TelemetryNames.Retries, "{retry}",
            "A failed attempt that was retried");
        this._workerItems = this.Meter.CreateCounter<long>(TelemetryNames.WorkerItemsCounter, "{item}",
            "An item a worker attempted");
        this._guildLogSkipped = this.Meter.CreateCounter<long>(TelemetryNames.GuildLogSkipped, "{message}",
            "A guild log nobody would see, so it wasn't built");
        this._guildLogPosts = this.Meter.CreateCounter<long>(TelemetryNames.GuildLogPosts, "{message}",
            "A guild log post the publisher finished");
        this._guildIssues = this.Meter.CreateCounter<long>(TelemetryNames.GuildIssues, "{issue}",
            "A guild issue reported for a refused permission");
        this._pluralKitQueueFull = this.Meter.CreateCounter<long>(TelemetryNames.PluralKitQueueFull, "{lookup}",
            "A PluralKit lookup refused because the rate limiter's queue was full");
        this._alerts = this.Meter.CreateCounter<long>(TelemetryNames.Alerts, "{alert}", "An alert raised");
    }

    public Meter Meter { get; }

    public void Dispose() => this.Meter.Dispose();

    /// <summary>The metric a finished span records, by its kind. A command's comes from CommandHandler instead.</summary>
    internal void Record(Operation operation, SpanEnd end, TimeSpan elapsed)
    {
        var outcome = new KeyValuePair<string, object?>(TelemetryNames.Outcome, end.Outcome);
        switch (operation.Kind)
        {
            case OperationKind.Event:
                this._eventDuration.Record(elapsed.TotalSeconds, Tag(operation, TelemetryNames.Handler), outcome);
                break;
            case OperationKind.WorkerTick:
                this._workerTickDuration.Record(elapsed.TotalSeconds, Tag(operation, TelemetryNames.Worker), outcome);
                break;
            case OperationKind.WorkerItem:
                this._workerItems.Add(1, Tag(operation, TelemetryNames.Worker), outcome);
                break;
            case OperationKind.GuildLogPost:
                this._guildLogPosts.Add(1, Tag(operation, TelemetryNames.GuildLogType), outcome);
                break;
            case OperationKind.Command:
                break;
            default:
                this._callDuration.Record(elapsed.TotalSeconds,
                    new KeyValuePair<string, object?>(TelemetryNames.Dependency, operation.Dependency.OrNull()),
                    new KeyValuePair<string, object?>(TelemetryNames.OperationName, operation.Name),
                    outcome);
                break;
        }
    }

    internal void Count(Counter counter, Tags tags)
    {
        var pairs = LiveTelemetryIO.ToPairs(tags).ToArray();
        var instrument = counter switch
        {
            Counter.Retries => this._retries,
            Counter.GuildLogSkipped => this._guildLogSkipped,
            Counter.GuildIssues => this._guildIssues,
            _ => null
        };
        instrument?.Add(1, pairs);
    }

    /// <summary>Every command, ported or not, from where DSharpPlus reports it finished (spec 1.6, section 3.7).</summary>
    public void RecordCommand(string command, string invocation, string outcome, TimeSpan elapsed) =>
        this._commandDuration.Record(elapsed.TotalSeconds,
            new KeyValuePair<string, object?>(TelemetryNames.Command, command),
            new KeyValuePair<string, object?>(TelemetryNames.Invocation, invocation),
            new KeyValuePair<string, object?>(TelemetryNames.Outcome, outcome));

    public void RecordPluralKitQueueWait(TimeSpan wait) => this._pluralKitQueueWait.Record(wait.TotalSeconds);

    public void CountPluralKitQueueFull() => this._pluralKitQueueFull.Add(1);

    public void CountAlert(string severity, string type, bool dropped) =>
        this._alerts.Add(1,
            new KeyValuePair<string, object?>(TelemetryNames.AlertSeverity, severity),
            new KeyValuePair<string, object?>(TelemetryNames.AlertType, type),
            new KeyValuePair<string, object?>(TelemetryNames.AlertDropped, dropped));

    private static KeyValuePair<string, object?> Tag(Operation operation, string name) =>
        new(name, operation.Tags.Find(name).OrNull());
}

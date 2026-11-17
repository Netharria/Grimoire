// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Functional.Capabilities;
using LanguageExt;

namespace Grimoire.Functional.Telemetry;

/// <summary>
///     A capability call inside its span, retried or not (spec 1.6, section 3.7). Used by <c>Db</c>, <c>Discord</c>
///     and <c>PluralKit</c>, so a feature gets its spans without asking.
/// </summary>
internal static class Observed
{
    /// <summary>One span around the call. Never retried.</summary>
    public static IO<A> Call<A>(ITelemetryIO telemetry, Operation operation, IO<A> call,
        Func<A, string>? outcomeOf = null) =>
        telemetry.Span(operation, call, Ended(outcomeOf));

    /// <summary>
    ///     One span around every attempt. Each retry adds a <c>retry</c> event (its attempt number and the previous
    ///     attempt's tags) and counts one <c>grimoire.call.retries</c>, so the events match the retries exactly.
    /// </summary>
    public static IO<A> Retried<A>(ITelemetryIO telemetry, Operation operation, IO<A> attempt, Schedule schedule,
        Func<Error, bool> isTransient, Func<A, string>? outcomeOf = null) =>
        telemetry.Span(operation, Retrying(telemetry, operation, attempt, schedule, isTransient), Ended(outcomeOf));

    /// <summary>The retry loop with its events, without a span of its own: for a caller that opens the span.</summary>
    public static IO<A> Retrying<A>(ITelemetryIO telemetry, Operation operation, IO<A> attempt, Schedule schedule,
        Func<Error, bool> isTransient) =>
        IO.lift(() => new Attempts()).Bind(attempts =>
            IO.lift(() => attempts.Next())
                .Bind(number => number > 1 ? Retry(telemetry, operation, number, attempts.Last) : IO.pure(unit))
                .Bind(_ => attempt)
                .Catch(_ => true, error => IO.lift(() => attempts.Failed(error)).Bind(_ => IO.fail<A>(error)))
                .RetryWhile(schedule, isTransient));

    private static IO<Unit> Retry(ITelemetryIO telemetry, Operation operation, int number, Option<Error> previous)
    {
        var failed = previous.Map(error => TelemetryTags.Of(error)).IfNone(Tags.Empty);
        return telemetry.Event(TelemetryNames.RetryEvent, Tags.Of((TelemetryNames.Attempt, number)).Add(failed))
            .Bind(_ => telemetry.Count(Counter.Retries, Tags.Of(
                (TelemetryNames.Dependency, operation.Dependency.OrNull()),
                (TelemetryNames.OperationName, operation.Name),
                (TelemetryNames.Outcome, failed.Find(TelemetryNames.Outcome).OrNull()))));
    }

    private static Func<A, SpanEnd> Ended<A>(Func<A, string>? outcomeOf) =>
        outcomeOf is null ? _ => SpanResult.Succeeded() : value => SpanResult.Succeeded(outcomeOf(value));

    /// <summary>The attempt count and the last failure, for one run of the call. Only the retry loop touches it.</summary>
    private sealed class Attempts
    {
        private int _count;

        public Option<Error> Last { get; private set; }

        public int Next() => Interlocked.Increment(ref this._count);

        public Unit Failed(Error error)
        {
            this.Last = error;
            return unit;
        }
    }
}

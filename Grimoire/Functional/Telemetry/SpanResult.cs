// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Functional.Boundary;
using Grimoire.Functional.Data;
using Grimoire.Functional.Discord;
using Grimoire.Functional.PluralKit;
using LanguageExt;

namespace Grimoire.Functional.Telemetry;

/// <summary>How a finished operation looks in its span and its metric.</summary>
/// <param name="Outcome">The <c>grimoire.outcome</c> tag: a <c>CommandOutcome</c> name, or a value's own label.</param>
/// <param name="IsError">Status error: only an outage or a defect. A refusal isn't a fault.</param>
/// <param name="Tags">The tags read from the error, if any.</param>
/// <param name="ErrorType">The <c>error.type</c> tag, set only when <paramref name="IsError" />.</param>
/// <param name="Exception">The exception to record on the span, for outages and defects.</param>
public sealed record SpanEnd(
    string Outcome,
    bool IsError,
    Tags Tags,
    Option<string> ErrorType,
    Option<Exception> Exception);

/// <summary>The outcome, status and tags of a finished operation (spec 1.6, section 3.2). Pure.</summary>
public static class SpanResult
{
    public const string Success = "Success";

    public static SpanEnd Of<A>(Fin<A> result, bool shuttingDown, Func<A, SpanEnd> ended) =>
        result.Match(Succ: ended, Fail: error => OfError(error, shuttingDown));

    /// <summary>A success, labelled: "Success", or a value's own case such as <c>NotProxied</c>.</summary>
    public static SpanEnd Succeeded(string outcome = Success) => new(outcome, false, Tags.Empty, None, None);

    /// <summary>For a root span, whose result is the boundary's outcome: the child that failed has the details.</summary>
    public static SpanEnd Of(CommandOutcome outcome) =>
        new(outcome.ToString(), IsFault(outcome), Tags.Empty,
            IsFault(outcome) ? Some(outcome.ToString()) : None, None);

    public static SpanEnd OfError(Error error, bool shuttingDown)
    {
        var outcome = Boundary.Outcome.Classify(error, shuttingDown);
        if (outcome is CommandOutcome.Cancelled)
            return new SpanEnd(outcome.ToString(), false, Tags.Empty, None, None);

        var tags = TelemetryTags.Of(error, shuttingDown);
        if (!IsFault(outcome))
            return new SpanEnd(outcome.ToString(), false, tags, None, None);

        var worst = Boundary.Outcome.WorstOf(error, shuttingDown);
        var exception = Boundary<Runtime.GrimoireRuntime>.ExceptionOf(worst);
        var errorType = outcome is CommandOutcome.Defect
            ? exception.Map(value => value.GetType().Name).IfNone(nameof(CommandOutcome.Defect))
            : outcome.ToString();
        return new SpanEnd(outcome.ToString(), true, tags, errorType, exception);
    }

    private static bool IsFault(CommandOutcome outcome) =>
        outcome is CommandOutcome.Unavailable or CommandOutcome.Defect;
}

/// <summary>
///     The tags a failure adds to its span (spec 1.6, section 3.3). Reads typed fields only, never a message: a
///     refusal's message is shown to the user and may name someone, and Postgres's can quote a value. Pure.
/// </summary>
public static class TelemetryTags
{
    public static Tags Of(Error error, bool shuttingDown = false)
    {
        var worst = Boundary.Outcome.WorstOf(error, shuttingDown);
        var outcome = Boundary.Outcome.Classify(error, shuttingDown);
        var tags = Tags.Of((TelemetryNames.Outcome, outcome.ToString()));

        if (worst is Forbidden forbidden)
            tags = tags.Add(TelemetryNames.Permission, forbidden.Refusal.Map(refusal => refusal.Permission).OrNull());

        return Boundary<Runtime.GrimoireRuntime>.ExceptionOf(worst).Match(
            exception => tags.Add(FromException(exception)),
            () => tags);
    }

    private static Tags FromException(Exception exception) =>
        exception switch
        {
            DatabaseException database => Tags.Of(
                (TelemetryNames.DbStatus, database.SqlState),
                (TelemetryNames.DbTable, database.Table),
                (TelemetryNames.DbConstraint, database.Constraint),
                (TelemetryNames.Transient, database.IsTransient)),
            DiscordCallException discord => Tags.Of(
                (TelemetryNames.HttpStatus, discord.Status),
                (TelemetryNames.DiscordCode, discord.Code),
                (TelemetryNames.Transient, discord.IsTransient)),
            PluralKitCallException pluralKit => Tags.Of(
                (TelemetryNames.PluralKitProblem, pluralKit.Problem.ToString()),
                (TelemetryNames.HttpStatus, pluralKit.Status),
                (TelemetryNames.Transient, pluralKit.IsTransient)),
            _ => Tags.Empty
        };
}

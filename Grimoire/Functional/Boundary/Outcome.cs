// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Functional.Boundary;

/// <summary>How a command run ended: for the reply, the "finished" log line and (in section 1.6) the span tag.</summary>
public enum CommandOutcome
{
    Success,

    /// <summary>A private command run as text: reacted 🔒 and didn't run.</summary>
    PrivateOnText,
    Invalid,
    NotFound,
    Forbidden,
    Unavailable,
    Defect,

    /// <summary>The bot is shutting down.</summary>
    Cancelled,

    // Set by CommandHandler for failures DSharpPlus reports before the command method runs, or for old commands.
    CheckFailed,
    ParseError,
    BotMissingPermissions
}

public static class Outcome
{
    /// <summary>
    ///     The case of an error. A <c>ManyErrors</c> takes its worst case: Cancelled (shutdown), then Defect, then
    ///     Unavailable, then Forbidden, then Invalid, then NotFound. Any error that isn't one of the four Grimoire cases
    ///     is a defect (plan C1), including language-ext's <c>Errors.None</c> from an <c>Option</c> bound without
    ///     <c>Effect.Require</c>. A cancellation is <c>Cancelled</c> only while the bot shuts down; otherwise it, and a
    ///     timeout, mean a dependency was too slow: <c>Unavailable</c>.
    /// </summary>
    [Pure]
    public static CommandOutcome Classify(Error error, bool shuttingDown) =>
        error.AsIterable().Select(single => CaseOf(single, shuttingDown)).ToArray() switch
        {
            // Some language-ext errors (Errors.None among them) iterate as nothing at all: still a defect.
            [] => CommandOutcome.Defect,
            var cases => cases.MaxBy(Severity)
        };

    /// <summary>True for the cases whose message is shown to the user.</summary>
    [Pure]
    public static bool IsRefusal(CommandOutcome outcome) =>
        outcome is CommandOutcome.Invalid or CommandOutcome.NotFound or CommandOutcome.Forbidden;

    private static CommandOutcome CaseOf(Error error, bool shuttingDown) =>
        error switch
        {
            Invalid => CommandOutcome.Invalid,
            NotFound => CommandOutcome.NotFound,
            Forbidden => CommandOutcome.Forbidden,
            Unavailable => CommandOutcome.Unavailable,
            _ when IsCancellation(error) => shuttingDown ? CommandOutcome.Cancelled : CommandOutcome.Unavailable,
            _ when error.Is(Errors.TimedOut) || error.HasException<TimeoutException>() => CommandOutcome.Unavailable,
            // The defect rule itself, not a hidden case: every other Error is a bug (C1).
            _ => CommandOutcome.Defect
        };

    private static bool IsCancellation(Error error) =>
        error.Is(Errors.Cancelled) || error.HasException<OperationCanceledException>();

    private static int Severity(CommandOutcome outcome) =>
        outcome switch
        {
            CommandOutcome.Cancelled => 6,
            CommandOutcome.Defect => 5,
            CommandOutcome.Unavailable => 4,
            CommandOutcome.Forbidden => 3,
            CommandOutcome.Invalid => 2,
            CommandOutcome.NotFound => 1,
            _ => 0
        };
}

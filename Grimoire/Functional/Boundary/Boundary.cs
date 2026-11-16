// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Effects;
using LanguageExt;
using LanguageExt.Traits;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Boundary;

/// <summary>Helpers the three boundaries share.</summary>
public static class Boundary<RT> where RT : Has<Eff<RT>, ILogIO>
{
    /// <summary>Runs <paramref name="effect" /> and returns its result as a value, so the caller can branch on it.</summary>
    public static Eff<RT, Fin<A>> Attempt<A>(Eff<RT, A> effect) =>
        effect.Match(Succ: Fin.Succ, Fail: Fin.Fail<A>);

    /// <summary>
    ///     Runs a step whose failure only needs a log line: a reply after the alert already went, or a reaction. The
    ///     result is success either way.
    /// </summary>
    public static Eff<RT, Unit> Quietly(Eff<RT, Unit> effect, string category, string template, params object?[] args)
        => Attempt(effect).Bind(result => result.Match(
            Succ: Eff<RT, Unit>.Pure,
            Fail: error => Log<RT>.Warning(category, ExceptionOf(error), template + ": {Reason}",
                [..args, error.Message])));

    /// <summary>True once the run's token is cancelled, which only the host's stopping token does.</summary>
    public static Eff<RT, bool> ShuttingDown =>
        Eff<RT, bool>.LiftIO(IO.lift(env => env.Token.IsCancellationRequested));

    /// <summary>
    ///     The first exception inside an error, including one an <c>Unavailable</c> keeps as its inner error, for the
    ///     log line and the alert's key.
    /// </summary>
    public static Option<Exception> ExceptionOf(Error error) =>
        error.AsIterable()
            .Select(single => single.Exception || single.Inner.Bind(inner => inner.Exception))
            .FirstOrDefault(exception => exception.IsSome);
}

/// <summary>The guild-issue half of a Discord refusal, shared by the three boundaries (spec 1.4, section 3.7).</summary>
public static class BotRefusals<RT> where RT : Has<Eff<RT>, ILogIO>, Has<Eff<RT>, IGuildIssueIO>
{
    /// <summary>
    ///     Tells the guild about every permission Discord refused the bot inside <paramref name="error" />, one report
    ///     per distinct issue. A refusal by our own rules, or a member's closed DMs, carries no <c>BotRefusal</c> and
    ///     reports nothing. A failed report is only logged: the outcome stays the same.
    /// </summary>
    public static Eff<RT, Unit> Report(Error error, string category) =>
        Of(error).Fold(Effect.Done<RT>(), (reports, issue) => reports.Bind(_ => Boundary<RT>.Quietly(
            GuildIssues<RT>.Report(issue), category, "Could not report {Code} to guild {GuildId}", issue.Code,
            issue.GuildId.Value)));

    /// <summary>The issues to report, in order, without repeats. Pure.</summary>
    public static Seq<GuildIssue> Of(Error error) =>
        toSeq(error.AsIterable()
            .SelectMany(single => single is Forbidden forbidden ? forbidden.Refusal.ToSeq() : [])
            .Select(GuildIssueCatalog.BotMissingPermission)
            .DistinctBy(issue => (issue.GuildId, issue.Code)));
}

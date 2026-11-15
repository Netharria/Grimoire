// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Functional.Capabilities;
using LanguageExt;
using LanguageExt.Traits;
using Microsoft.Extensions.Logging;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Effects;

// The functions feature code calls. Each module is generic in the runtime and names the one capability it needs, so a
// function's where-clause lists exactly the effects it can have: code constrained only by Has<Eff<RT>, ITimeIO> can't
// reply to a command, because that call wouldn't compile.

public static class Clock<RT> where RT : Has<Eff<RT>, ITimeIO>
{
    private static Eff<RT, ITimeIO> Trait => Has<Eff<RT>, RT, ITimeIO>.ask.As();

    public static Eff<RT, DateTimeOffset> UtcNow => Trait.Bind(time => time.UtcNow);
    public static Eff<RT, long> Timestamp => Trait.Bind(time => time.Timestamp);
    public static Eff<RT, TimeSpan> ElapsedSince(long timestamp) => Trait.Bind(time => time.ElapsedSince(timestamp));
}

public static class Log<RT> where RT : Has<Eff<RT>, ILogIO>
{
    private static Eff<RT, ILogIO> Trait => Has<Eff<RT>, RT, ILogIO>.ask.As();

    public static Eff<RT, Unit> Write(LogEntry entry) => Trait.Bind(log => log.Write(entry));

    public static Eff<RT, Unit> Debug(string category, string template, params object?[] args) =>
        Write(new LogEntry(LogLevel.Debug, category, template, toSeq(args), None));

    public static Eff<RT, Unit> Information(string category, string template, params object?[] args) =>
        Write(new LogEntry(LogLevel.Information, category, template, toSeq(args), None));

    public static Eff<RT, Unit> Warning(string category, Option<Exception> exception, string template,
        params object?[] args) =>
        Write(new LogEntry(LogLevel.Warning, category, template, toSeq(args), exception));

    public static Eff<RT, Unit> Error(string category, Option<Exception> exception, string template,
        params object?[] args) =>
        Write(new LogEntry(LogLevel.Error, category, template, toSeq(args), exception));
}

public static class Alerts<RT> where RT : Has<Eff<RT>, IAlertIO>
{
    public static Eff<RT, Unit> Send(Alert alert) =>
        Has<Eff<RT>, RT, IAlertIO>.ask.As().Bind(alerts => alerts.Send(alert));
}

public static class GuildLogs<RT> where RT : Has<Eff<RT>, IGuildLogIO>
{
    public static Eff<RT, Unit> Send(GuildLogMessageBase message) =>
        Has<Eff<RT>, RT, IGuildLogIO>.ask.As().Bind(log => log.Send(message));
}

public static class Trace<RT> where RT : Has<Eff<RT>, ITraceIO>
{
    public static Eff<RT, string> ErrorId => Has<Eff<RT>, RT, ITraceIO>.ask.As().Bind(trace => trace.ErrorId);
}

public static class Command<RT> where RT : Has<Eff<RT>, ICommandIO>
{
    private static Eff<RT, ICommandIO> Trait => Has<Eff<RT>, RT, ICommandIO>.ask.As();

    public static Eff<RT, Unit> Defer(Delivery delivery) => Trait.Bind(command => command.Defer(delivery));

    public static Eff<RT, Unit> Reply(Delivery delivery, DiscordEmbed embed, Seq<DiscordEmbed> followUps) =>
        Trait.Bind(command => command.Reply(delivery, embed, followUps));

    public static Eff<RT, Unit> Refuse(Delivery delivery, DiscordEmbed embed) =>
        Trait.Bind(command => command.Refuse(delivery, embed));

    public static Eff<RT, Unit> React(string emoji) => Trait.Bind(command => command.React(emoji));
}

/// <summary>Small combinators the language-ext prelude doesn't have in this shape.</summary>
public static class Effect
{
    /// <summary>An effect that does nothing, for the branch that has no work.</summary>
    public static Eff<RT, Unit> Done<RT>() => Eff<RT, Unit>.Pure(unit);

    /// <summary>Runs <paramref name="effect" /> on the value if there is one; otherwise does nothing.</summary>
    public static Eff<RT, Unit> ForSome<RT, A>(Option<A> option, Func<A, Eff<RT, Unit>> effect) =>
        option.Match(effect, Done<RT>);

    /// <summary>Lifts a validation into an effect: valid continues, invalid fails with every collected error.</summary>
    public static Eff<RT, A> Validated<RT, A>(Validation<Error, A> validation) =>
        validation.Match(Succ: Eff<RT, A>.Pure, Fail: Eff<RT, A>.Fail);

    /// <summary>
    ///     Turns an absent value into an error case. Bind an <c>Option</c> through this, never directly in a
    ///     <c>from</c> clause: language-ext would fail with its own <c>Errors.None</c>, which the boundary treats as a
    ///     defect.
    /// </summary>
    public static Eff<RT, A> Require<RT, A>(Option<A> option, Error whenNone) =>
        option.Match(Eff<RT, A>.Pure, () => Eff<RT, A>.Fail(whenNone));
}

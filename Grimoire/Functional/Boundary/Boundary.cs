// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

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

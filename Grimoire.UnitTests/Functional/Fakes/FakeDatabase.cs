// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Data;
using LanguageExt;

namespace Grimoire.UnitTests.Functional.Fakes;

/// <summary>
///     A database that answers by statement name (spec 1.3, section 3.11). A statement with no answer fails with its
///     name, so no pipeline runs against an accidental empty result. Every call is recorded, in order, in the
///     shared <see cref="Journal" /> as <c>db Name</c>; a transaction adds <c>begin</c> and <c>commit</c> or
///     <c>rollback</c> around its statements.
/// </summary>
public sealed class FakeDatabase(Journal journal) : IDatabaseIO, IDatabaseSession
{
    private readonly Atom<HashMap<string, Func<Sql, object>>> _answers = Atom(HashMap<string, Func<Sql, object>>());
    private readonly Atom<Seq<Sql>> _calls = Atom(Seq<Sql>());
    private readonly Atom<HashMap<string, (Error Error, int Remaining)>> _failures =
        Atom(HashMap<string, (Error Error, int Remaining)>());

    /// <summary>Every statement run, in order, with its parameters.</summary>
    public Seq<Sql> Calls => this._calls.Value;

    /// <summary>The same three retries as production, without the waits.</summary>
    public Schedule ReadRetry => Schedule.recurs(3);

    /// <summary>The rows a query returns.</summary>
    public FakeDatabase Returns<A>(string name, params A[] rows) => this.Answer(name, _ => toSeq(rows));

    /// <summary>The rows a query returns, worked out from the statement (its parameters, say).</summary>
    public FakeDatabase Returns<A>(string name, Func<Sql, Seq<A>> rows) => this.Answer(name, sql => rows(sql));

    /// <summary>The affected-row count an <c>Execute</c> returns.</summary>
    public FakeDatabase Affects(string name, int rows) => this.Answer(name, _ => rows);

    /// <summary>Fails the statement with <paramref name="error" />, the first <paramref name="times" /> times it runs.</summary>
    public FakeDatabase Fails(string name, Error error, int times = int.MaxValue)
    {
        this._failures.Swap(failures => failures.AddOrUpdate(name, (error, times)));
        return this;
    }

    public IO<Seq<A>> Query<A>(Sql sql) => this.Call(sql).Map(answer => Cast<Seq<A>>(sql, answer));

    public IO<Option<A>> QueryOptional<A>(Sql sql) => this.Query<A>(sql).Map(rows => rows.Head);

    public IO<A> QuerySingle<A>(Sql sql) =>
        this.Query<A>(sql).Bind(rows => rows.Count == 1
            ? IO.pure(rows[0])
            : IO.fail<A>(Error.New(new InvalidOperationException(
                $"{sql.Name} returned {rows.Count} rows; QuerySingle needs exactly one."))));

    public IO<int> Execute(Sql sql) => this.Call(sql).Map(answer => Cast<int>(sql, answer));

    public IO<A> Transaction<A>(Func<IDatabaseSession, IO<A>> body,
        IsolationLevel isolation = IsolationLevel.ReadCommitted) =>
        IO.lift(() => journal.Add("begin"))
            .Bind(_ => body(this))
            .Map(result =>
            {
                journal.Add("commit");
                return result;
            })
            .Catch(_ => true, error =>
            {
                journal.Add("rollback");
                return IO.fail<A>(error);
            }).As();

    public IO<S> Fold<A, S>(Sql sql, S seed, Func<S, A, S> step) =>
        this.Query<A>(sql).Map(rows => rows.Fold(seed, step));

    private FakeDatabase Answer(string name, Func<Sql, object> answer)
    {
        this._answers.Swap(answers => answers.AddOrUpdate(name, answer));
        return this;
    }

    private IO<object> Call(Sql sql) =>
        IO.lift<Fin<object>>(() =>
        {
            journal.Add($"db {sql.Name}");
            this._calls.Swap(calls => calls.Add(sql));

            if (this.TakeFailure(sql.Name).Case is Error error)
                return Fin.Fail<object>(error);

            return this._answers.Value.Find(sql.Name).Case is Func<Sql, object> answer
                ? Fin.Succ(answer(sql))
                : throw new InvalidOperationException($"FakeDatabase has no answer for {sql.Name}.");
        }).Bind(result => result.Match(Succ: IO.pure, Fail: IO.fail<object>));

    private Option<Error> TakeFailure(string name)
    {
        if (this._failures.Value.Find(name).Case is not (Error error, int remaining and > 0))
            return None;

        this._failures.Swap(failures => failures.SetItem(name, (error, remaining - 1)));
        return error;
    }

    private static T Cast<T>(Sql sql, object answer) =>
        answer is T typed
            ? typed
            : throw new InvalidOperationException(
                $"FakeDatabase's answer for {sql.Name} is a {answer.GetType().Name}, not a {typeof(T).Name}.");
}

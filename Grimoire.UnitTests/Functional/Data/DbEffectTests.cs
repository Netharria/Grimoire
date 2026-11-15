// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Functional.Data;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;

namespace Grimoire.UnitTests.Functional.Data;

/// <summary><c>Db&lt;RT&gt;</c> against the fake database: which calls are retried, and transactions.</summary>
public sealed class DbEffectTests
{
    private static readonly Sql Read = Sql.Read("Test.Count", "SELECT count(*) FROM x");
    private static readonly Sql Write = Sql.Write("Test.Update", "UPDATE x SET y = 1");
    private static readonly Sql Returning = Sql.Write("Test.Insert", "INSERT INTO x DEFAULT VALUES RETURNING id");

    private readonly TestEnv _env = new();

    private static Error Transient(Sql sql) =>
        DatabaseErrors.Classify(new TimeoutException(), sql, false, "db:5432", CancellationToken.None);

    private static Error Defect(Sql sql) =>
        DatabaseErrors.Classify(DatabaseErrorsTests.Postgres("42703"), sql, false, "db:5432", CancellationToken.None);

    [Fact]
    public async Task ARead_ThatFailsTwice_IsRetried_AndSucceeds()
    {
        this._env.Database.Returns(Read.Name, 7L).Fails(Read.Name, Transient(Read), times: 2);

        (await this._env.Run(Db<GrimoireRuntime>.QuerySingle<long>(Read))).ShouldSucceed().ShouldBe(7L);

        this._env.Database.Calls.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ARead_ThatKeepsFailing_IsUnavailable_AfterThreeRetries()
    {
        this._env.Database.Returns(Read.Name, 7L).Fails(Read.Name, Transient(Read));

        (await this._env.Run(Db<GrimoireRuntime>.QuerySingle<long>(Read))).ShouldFail().ShouldBeOfType<Unavailable>();

        this._env.Database.Calls.Count.ShouldBe(4);
    }

    [Fact]
    public async Task ARead_ThatFailsWithADefect_IsNotRetried()
    {
        this._env.Database.Returns(Read.Name, 7L).Fails(Read.Name, Defect(Read), times: 1);

        (await this._env.Run(Db<GrimoireRuntime>.Query<long>(Read))).ShouldFail();

        this._env.Database.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AWrite_IsNeverRetried()
    {
        this._env.Database.Affects(Write.Name, 1).Fails(Write.Name, Transient(Write), times: 1);

        (await this._env.Run(Db<GrimoireRuntime>.Execute(Write))).ShouldFail().ShouldBeOfType<Unavailable>();

        this._env.Database.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AWriteWithReturning_ThroughQuery_IsNotRetried()
    {
        this._env.Database.Returns(Returning.Name, 1L).Fails(Returning.Name, Transient(Returning), times: 1);

        (await this._env.Run(Db<GrimoireRuntime>.Query<long>(Returning))).ShouldFail();

        this._env.Database.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public async Task QueryOptional_IsNone_WhenThereIsNoRow()
    {
        this._env.Database.Returns<long>(Read.Name);

        (await this._env.Run(Db<GrimoireRuntime>.QueryOptional<long>(Read))).ShouldSucceed().ShouldBe(None);
    }

    [Fact]
    public async Task AStatementWithNoAnswer_FailsWithItsName()
    {
        var result = await this._env.Run(Db<GrimoireRuntime>.Query<long>(Write));

        result.ShouldFail().Message.ShouldContain(Write.Name);
    }

    [Fact]
    public async Task ATransaction_CommitsWhenTheBodySucceeds()
    {
        this._env.Database.Returns(Returning.Name, 42L).Affects(Write.Name, 1);

        var result = await this._env.Run(Db<GrimoireRuntime>.Transaction(session =>
            from id in session.QuerySingle<long>(Returning)
            from _ in session.Execute(Write)
            select id));

        result.ShouldSucceed().ShouldBe(42L);
        this._env.Journal.Entries.ShouldBe(["begin", $"db {Returning.Name}", $"db {Write.Name}", "commit"]);
    }

    [Fact]
    public async Task ATransaction_RollsBack_WhenAStatementFails_AndIsNotRetried()
    {
        this._env.Database.Returns(Returning.Name, 42L).Fails(Returning.Name, Transient(Returning), times: 1);

        var result = await this._env.Run(Db<GrimoireRuntime>.Transaction(session =>
            session.QuerySingle<long>(Returning)));

        result.ShouldFail().ShouldBeOfType<Unavailable>();
        this._env.Journal.Entries.ShouldBe(["begin", $"db {Returning.Name}", "rollback"]);
    }

    [Fact]
    public async Task ATransaction_KeepsTheBodysCase_WhenItRollsBack()
    {
        this._env.Database.Returns<long>(Read.Name);

        var result = await this._env.Run(Db<GrimoireRuntime>.Transaction(session =>
            session.QueryOptional<long>(Read).Bind(row => row.Match(
                IO.pure,
                () => IO.fail<long>(new NotFound("That sin doesn't exist."))))));

        result.ShouldFail().ShouldBeOfType<NotFound>().Message.ShouldBe("That sin doesn't exist.");
        this._env.Journal.Entries.ShouldContain("rollback");
    }

    [Fact]
    public async Task Fold_RunsThePureStepOverEveryRow()
    {
        this._env.Database.Returns(Read.Name, 1L, 2L, 3L);

        (await this._env.Run(Db<GrimoireRuntime>.Fold<long, long>(Read, 0, (sum, row) => sum + row)))
            .ShouldSucceed().ShouldBe(6L);
    }

    [Fact]
    public async Task CommandsHaveTheDatabaseToo()
    {
        this._env.Database.Returns(Read.Name, 7L);

        (await this._env.Run(Db<CommandRuntime>.QuerySingle<long>(Read))).ShouldSucceed().ShouldBe(7L);
    }
}

public sealed class DatabaseRetryTests
{
    private static readonly Sql Read = Sql.Read("Test.Read", "SELECT 1");

    [Fact]
    public void TheReadSchedule_IsThreeRetries_UnderASecond()
    {
        var delays = DatabaseRetry.Reads.Run().Take(10).Select(delay => (double)delay).ToList();

        delays.Count.ShouldBe(3);
        delays[0].ShouldBeInRange(90, 110);
        delays[1].ShouldBeInRange(180, 220);
        delays[2].ShouldBeInRange(360, 440);
        delays.Sum().ShouldBeLessThan(1000);
        // Jittered, so retries from many runs don't arrive together: exact powers of two would mean no jitter.
        delays.ShouldNotBe([100d, 200d, 400d]);
    }

    [Fact]
    public void OnlyTransientUnavailable_IsRetried()
    {
        DatabaseRetry.IsTransient(
                DatabaseErrors.Classify(new TimeoutException(), Read, false, "db:5432", CancellationToken.None))
            .ShouldBeTrue();

        DatabaseRetry.IsTransient(new Unavailable("Discord could not be reached.")).ShouldBeFalse();
        DatabaseRetry.IsTransient(new Invalid("bad")).ShouldBeFalse();
        DatabaseRetry.IsTransient(new NotFound("gone")).ShouldBeFalse();
        DatabaseRetry.IsTransient(Error.New(new InvalidOperationException())).ShouldBeFalse();
    }
}

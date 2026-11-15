// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data;
using Grimoire.Data.Dapper;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Data;
using LanguageExt;
using LanguageExt.Traits;
using Npgsql;

namespace Grimoire.IntegrationTests.Database.Dapper;

/// <summary>A runtime with only the database, for SQL tests: <c>Db&lt;RT&gt;</c> needs nothing else.</summary>
public sealed record DatabaseRuntime(IDatabaseIO Database) : Has<Eff<DatabaseRuntime>, IDatabaseIO>
{
    static K<Eff<DatabaseRuntime>, IDatabaseIO> Has<Eff<DatabaseRuntime>, IDatabaseIO>.Ask =>
        Eff<DatabaseRuntime, IDatabaseIO>.Lift(runtime => runtime.Database);
}

/// <summary>
///     Runs production <c>Sql</c> values against the test container through the live capability (spec 1.3, section
///     8). Every <see cref="Sql.IsRead" /> statement goes to a connection whose transactions are read-only, so a
///     statement marked as a read that writes fails its test (with <c>25006</c>, a defect). Transactions use a
///     normal connection.
/// </summary>
public sealed class DatabaseTestEnv : IAsyncDisposable
{
    private readonly NpgsqlDataSource _readOnly;
    private readonly NpgsqlDataSource _readWrite;

    public DatabaseTestEnv(string connectionString)
    {
        DapperTypeHandlers.Register();
        this._readWrite = NpgsqlDataSource.Create(connectionString);
        this._readOnly = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(connectionString)
        {
            Options = "-c default_transaction_read_only=on"
        }.ConnectionString);
        this.Database = new ReadOnlyCheckedDatabase(new NpgsqlDatabaseIO(this._readOnly),
            new NpgsqlDatabaseIO(this._readWrite));
    }

    public IDatabaseIO Database { get; }

    public async Task<Fin<A>> Run<A>(Eff<DatabaseRuntime, A> effect, CancellationToken token = default)
    {
        using var envIO = EnvIO.New(token: token);
        return await effect.RunAsync(new DatabaseRuntime(this.Database), envIO);
    }

    public async ValueTask DisposeAsync()
    {
        await this._readOnly.DisposeAsync();
        await this._readWrite.DisposeAsync();
    }

    private sealed class ReadOnlyCheckedDatabase(IDatabaseIO readOnly, IDatabaseIO readWrite) : IDatabaseIO
    {
        public Schedule ReadRetry => readWrite.ReadRetry;

        public IO<Seq<A>> Query<A>(Sql sql) => this.For(sql).Query<A>(sql);
        public IO<Option<A>> QueryOptional<A>(Sql sql) => this.For(sql).QueryOptional<A>(sql);
        public IO<A> QuerySingle<A>(Sql sql) => this.For(sql).QuerySingle<A>(sql);
        public IO<int> Execute(Sql sql) => this.For(sql).Execute(sql);
        public IO<S> Fold<A, S>(Sql sql, S seed, Func<S, A, S> step) => this.For(sql).Fold(sql, seed, step);

        public IO<A> Transaction<A>(Func<IDatabaseSession, IO<A>> body,
            IsolationLevel isolation = IsolationLevel.ReadCommitted) => readWrite.Transaction(body, isolation);

        private IDatabaseIO For(Sql sql) => sql.IsRead ? readOnly : readWrite;
    }
}

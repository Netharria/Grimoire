// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data;
using Dapper;
using Grimoire.Functional.Capabilities;
using LanguageExt;
using Npgsql;

namespace Grimoire.Functional.Data;

/// <summary>
///     The live database capability: Dapper over the one pooled <see cref="NpgsqlDataSource" /> (spec 1.3). The only
///     runtime code that touches Npgsql. Every call passes the run's token, so shutdown cancels a running statement.
/// </summary>
internal sealed class NpgsqlDatabaseIO(NpgsqlDataSource dataSource) : IDatabaseIO
{
    private static readonly Sql Begin = Sql.Write("Database.Begin", "BEGIN");
    private static readonly Sql Commit = Sql.Write("Database.Commit", "COMMIT");

    private readonly string _server = Server(dataSource.ConnectionString);

    public Schedule ReadRetry => DatabaseRetry.Reads;

    public IO<Seq<A>> Query<A>(Sql sql) =>
        this.WithConnection(sql, (connection, token) => Statements.Query<A>(connection, null, sql, token));

    public IO<Option<A>> QueryOptional<A>(Sql sql) =>
        this.WithConnection(sql, (connection, token) => Statements.QueryOptional<A>(connection, null, sql, token));

    public IO<A> QuerySingle<A>(Sql sql) =>
        this.WithConnection(sql, (connection, token) => Statements.QuerySingle<A>(connection, null, sql, token));

    public IO<int> Execute(Sql sql) =>
        this.WithConnection(sql, (connection, token) => Statements.Execute(connection, null, sql, token));

    public IO<A> Transaction<A>(Func<IDatabaseSession, IO<A>> body,
        IsolationLevel isolation = IsolationLevel.ReadCommitted) =>
        this.Attempt(Begin, true, async token =>
            {
                var connection = await dataSource.OpenConnectionAsync(token);
                try
                {
                    return new Session(connection, await connection.BeginTransactionAsync(isolation, token),
                        this._server);
                }
                catch
                {
                    await connection.DisposeAsync();
                    throw;
                }
            })
            .Bracket(
                session => body(session).Bind(result =>
                    this.Attempt(Commit, true, async token =>
                    {
                        await session.Transaction.CommitAsync(token);
                        return result;
                    })),
                // Disposing a transaction that wasn't committed rolls it back.
                session => IO.liftAsync(async () =>
                {
                    await session.DisposeAsync();
                    return unit;
                }));

    public IO<S> Fold<A, S>(Sql sql, S seed, Func<S, A, S> step) =>
        this.WithConnection(sql, async (connection, token) =>
        {
            var state = seed;
            await foreach (var row in connection.QueryUnbufferedAsync<A>(sql.Text, sql.Parameters)
                               .WithCancellation(token))
                state = step(state, row);
            return state;
        });

    private IO<A> WithConnection<A>(Sql sql, Func<NpgsqlConnection, CancellationToken, Task<A>> work) =>
        this.Attempt(sql, false, async token =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(token);
            return await work(connection, token);
        });

    private IO<A> Attempt<A>(Sql sql, bool inTransaction, Func<CancellationToken, Task<A>> work) =>
        Attempt(sql, inTransaction, this._server, work);

    /// <summary>Runs the work, and turns any exception into a value through <see cref="DatabaseErrors" />.</summary>
    internal static IO<A> Attempt<A>(Sql sql, bool inTransaction, string server,
        Func<CancellationToken, Task<A>> work) =>
        IO.liftAsync(async env =>
        {
            try
            {
                return Fin.Succ(await work(env.Token));
            }
            catch (Exception exception)
            {
                return Fin.Fail<A>(DatabaseErrors.Classify(exception, sql, inTransaction, server, env.Token));
            }
        }).Bind(result => result.Match(Succ: IO.pure, Fail: IO.fail<A>));

    /// <summary>"host:port" for the error log. Never the connection string, which holds the password.</summary>
    internal static string Server(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return $"{builder.Host}:{builder.Port}";
    }

    private sealed class Session(NpgsqlConnection connection, NpgsqlTransaction transaction, string server)
        : IDatabaseSession, IAsyncDisposable
    {
        public NpgsqlTransaction Transaction => transaction;

        public IO<Seq<A>> Query<A>(Sql sql) =>
            Attempt(sql, true, server, token => Statements.Query<A>(connection, transaction, sql, token));

        public IO<Option<A>> QueryOptional<A>(Sql sql) =>
            Attempt(sql, true, server, token => Statements.QueryOptional<A>(connection, transaction, sql, token));

        public IO<A> QuerySingle<A>(Sql sql) =>
            Attempt(sql, true, server, token => Statements.QuerySingle<A>(connection, transaction, sql, token));

        public IO<int> Execute(Sql sql) =>
            Attempt(sql, true, server, token => Statements.Execute(connection, transaction, sql, token));

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    /// <summary>The Dapper calls, shared by plain statements and a transaction's session.</summary>
    private static class Statements
    {
        public static async Task<Seq<A>> Query<A>(NpgsqlConnection connection, NpgsqlTransaction? transaction,
            Sql sql, CancellationToken token) =>
            toSeq(await connection.QueryAsync<A>(Command(sql, transaction, token)));

        public static async Task<Option<A>> QueryOptional<A>(NpgsqlConnection connection,
            NpgsqlTransaction? transaction, Sql sql, CancellationToken token) =>
            (await connection.QueryAsync<A>(Command(sql, transaction, token))).AsIterable().Head;

        public static Task<A> QuerySingle<A>(NpgsqlConnection connection, NpgsqlTransaction? transaction, Sql sql,
            CancellationToken token) =>
            connection.QuerySingleAsync<A>(Command(sql, transaction, token));

        public static Task<int> Execute(NpgsqlConnection connection, NpgsqlTransaction? transaction, Sql sql,
            CancellationToken token) =>
            connection.ExecuteAsync(Command(sql, transaction, token));

        private static CommandDefinition Command(Sql sql, NpgsqlTransaction? transaction, CancellationToken token) =>
            new(sql.Text, sql.Parameters, transaction, cancellationToken: token);
    }
}

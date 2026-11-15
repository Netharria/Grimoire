// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Npgsql;

namespace Grimoire.Functional.Data;

/// <summary>
///     The one place a database exception becomes a value (Q1, spec 1.3 section 3.6). Pure: the live capability
///     catches, this decides.
/// </summary>
internal static class DatabaseErrors
{
    public const string UnavailableMessage = "The database is unavailable.";

    /// <summary>
    ///     Cancellation by the run's own token is <c>Errors.Cancelled</c>. An outage is <see cref="Unavailable" />,
    ///     marked transient when retrying a read could help. Anything else is a defect. Both keep a
    ///     <see cref="DatabaseException" /> that says what failed, without row or parameter values (3.6.1).
    /// </summary>
    /// <param name="server">"host:port", for a failure to connect. Never the connection string.</param>
    public static Error Classify(Exception exception, Sql sql, bool inTransaction, string server,
        CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return Errors.Cancelled;

        var (outage, transient) = Kind(exception);
        var diagnosed = Diagnose(exception, sql, inTransaction, server, transient);
        return outage ? Unavailable.From(UnavailableMessage, diagnosed) : Error.New(diagnosed);
    }

    /// <summary>Whether an exception is an outage, and whether it's one a retry could get past.</summary>
    internal static (bool Outage, bool Transient) Kind(Exception exception) => exception switch
    {
        PostgresException postgres => postgres.SqlState switch
        {
            // connection_exception, insufficient_resources
            var state when state.StartsWith("08", StringComparison.Ordinal)
                           || state.StartsWith("53", StringComparison.Ordinal) => (true, true),
            // admin_shutdown, crash_shutdown, cannot_connect_now, and query_canceled not from our token: a timeout
            PostgresErrorCodes.AdminShutdown or PostgresErrorCodes.CrashShutdown
                or PostgresErrorCodes.CannotConnectNow or PostgresErrorCodes.QueryCanceled => (true, true),
            // Only reads are retried, and these need a write or a serializable transaction, which aren't.
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected => (true, false),
            _ => (false, false)
        },
        // Npgsql marks a lost or refused connection and a timeout (an inner socket, IO or timeout exception) transient.
        NpgsqlException { IsTransient: true } => (true, true),
        TimeoutException or SocketException => (true, true),
        // Cancelled, but not by our token (checked first): Npgsql gave up on its own.
        OperationCanceledException => (true, true),
        _ => (false, false)
    };

    internal static DatabaseException Diagnose(Exception exception, Sql sql, bool inTransaction, string server,
        bool transient)
    {
        var how = (sql.IsRead ? "read, retried while transient" : "write, never retried")
                  + (inTransaction ? ", in a transaction" : string.Empty);
        var summary = new StringBuilder($"Database statement {sql.Name} failed ({how}):");
        var details = new StringBuilder();

        if (exception is PostgresException postgres)
        {
            summary.Append($"\n  {postgres.SqlState} {ConditionName(postgres.SqlState)}: {postgres.MessageText}")
                .Append($"\n  Object: schema {Quoted(postgres.SchemaName)}, table {Quoted(postgres.TableName)}, "
                        + $"column {Quoted(postgres.ColumnName)}, constraint {Quoted(postgres.ConstraintName)}, "
                        + $"type {Quoted(postgres.DataTypeName)}");
            details.Append($"  Hint: {Plain(postgres.Hint)} · Position: {(postgres.Position > 0 ? postgres.Position : "—")}"
                           + $" · Where: {Plain(postgres.Where)} · Routine: {Plain(postgres.Routine)}\n");
        }
        else
        {
            summary.Append($"\n  {exception.GetType().Name} talking to {server}: {Messages(exception)}");
        }

        details.Append($"  Parameters: {(sql.ParameterNames.IsEmpty ? "—" : string.Join(", ", sql.ParameterNames.Map(name => "@" + name)))}");
        return new DatabaseException(summary.ToString(), details.ToString(), transient, exception);
    }

    private static string Quoted(string? value) => string.IsNullOrEmpty(value) ? "—" : $"\"{value}\"";

    private static string Plain(string? value) => string.IsNullOrEmpty(value) ? "—" : value.ReplaceLineEndings(" | ");

    /// <summary>Each message down the inner-exception chain, as the socket or timeout says what really failed.</summary>
    private static string Messages(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(" → ", messages.Distinct());
    }

    /// <summary>"unique_violation" for 23505, from Npgsql's own list of codes.</summary>
    internal static string ConditionName(string sqlState) =>
        ConditionNames.TryGetValue(sqlState, out var name) ? name : "unknown_condition";

    private static readonly FrozenDictionary<string, string> ConditionNames = BuildConditionNames();

    /// <summary>Npgsql's <see cref="PostgresErrorCodes" /> constants, by code. Each code has one name in practice.</summary>
    internal static FrozenDictionary<string, string> BuildConditionNames() =>
        typeof(PostgresErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, FieldType.Name: nameof(String) })
            .GroupBy(field => (string)field.GetRawConstantValue()!)
            .ToFrozenDictionary(group => group.Key, group => SnakeCase(group.First().Name));

    internal static string SnakeCase(string pascal)
    {
        var builder = new StringBuilder(pascal.Length + 8);
        foreach (var character in pascal)
        {
            if (char.IsUpper(character) && builder.Length > 0)
                builder.Append('_');
            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}

/// <summary>
///     What went wrong with one statement, wrapping the Npgsql exception. Its <see cref="Exception.Message" /> is
///     the short form the alert shows: the statement, the SQLSTATE, Postgres's message, and the objects. Its
///     <see cref="ToString" />, which the log writes, adds the hint, position, context, routine and the parameter
///     names. Neither holds a parameter value or Postgres's <c>Detail</c> (spec 1.3, section 3.6.1).
/// </summary>
public sealed class DatabaseException : Exception
{
    internal DatabaseException(string summary, string details, bool isTransient, Exception inner)
        : base(summary, inner)
    {
        this.Details = details;
        this.IsTransient = isTransient;
    }

    /// <summary>The fields the log adds to <see cref="Exception.Message" />.</summary>
    public string Details { get; }

    /// <summary>True when retrying a read could get past it.</summary>
    public bool IsTransient { get; }

    public override string ToString() =>
        $"{this.GetType().FullName}: {this.Message}\n{this.Details}\n ---> {this.InnerException}";
}

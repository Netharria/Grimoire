// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Data;
using Npgsql;

namespace Grimoire.UnitTests.Functional.Data;

public sealed class DatabaseErrorsTests
{
    private const string Server = "db.example:5432";
    private static readonly Sql Read = Sql.Read("Test.Read", "SELECT 1");
    private static readonly Sql Write = Sql.Write("Test.Write", "UPDATE x SET y = 1", new { Id = 1 });

    internal static PostgresException Postgres(string sqlState, string message = "it failed") =>
        new(message, "ERROR", "ERROR", sqlState);

    /// <summary>The token is the run's own: cancelled only in the cancellation tests.</summary>
    private static Error Classify(Exception exception, Sql? sql = null, CancellationTokenSource? run = null) =>
        DatabaseErrors.Classify(exception, sql ?? Read, false, Server, run?.Token ?? CancellationToken.None);

    [Theory]
    [InlineData("08006")] // connection_failure
    [InlineData("08001")] // sqlclient_unable_to_establish_sqlconnection
    [InlineData("53300")] // too_many_connections
    [InlineData("57P01")] // admin_shutdown
    [InlineData("57P02")] // crash_shutdown
    [InlineData("57P03")] // cannot_connect_now
    [InlineData("57014")] // query_canceled, not by our token: a statement timeout
    public void OutagesFromPostgres_AreTransientUnavailable(string sqlState)
    {
        var error = Classify(Postgres(sqlState));

        error.ShouldBeOfType<Unavailable>().Message.ShouldBe(DatabaseErrors.UnavailableMessage);
        DatabaseRetry.IsTransient(error).ShouldBeTrue();
    }

    [Theory]
    [InlineData("40001")] // serialization_failure
    [InlineData("40P01")] // deadlock_detected
    public void SerializationAndDeadlock_AreUnavailable_ButNotRetried(string sqlState)
    {
        var error = Classify(Postgres(sqlState), Write);

        error.ShouldBeOfType<Unavailable>();
        DatabaseRetry.IsTransient(error).ShouldBeFalse();
    }

    [Theory]
    [InlineData("23505")] // unique_violation
    [InlineData("23503")] // foreign_key_violation
    [InlineData("23502")] // not_null_violation
    [InlineData("23514")] // check_violation
    [InlineData("42703")] // undefined_column
    [InlineData("42601")] // syntax_error
    [InlineData("22001")] // string_data_right_truncation
    [InlineData("22P02")] // invalid_text_representation
    public void ConstraintSyntaxAndDataErrors_AreDefects(string sqlState)
    {
        var error = Classify(Postgres(sqlState), Write);

        error.ShouldNotBeOfType<Unavailable>();
        var diagnosis = error.Exception.Case.ShouldBeOfType<DatabaseException>();
        diagnosis.InnerException.ShouldBeOfType<PostgresException>();
        diagnosis.IsTransient.ShouldBeFalse();
        Outcome.Classify(error, false).ShouldBe(CommandOutcome.Defect);
    }

    [Fact]
    public void ARefusedConnection_IsTransientUnavailable()
    {
        var exception = new NpgsqlException("Failed to connect to 10.0.0.1:5432",
            new SocketException((int)SocketError.ConnectionRefused));

        var error = Classify(exception);

        error.ShouldBeOfType<Unavailable>();
        DatabaseRetry.IsTransient(error).ShouldBeTrue();
        Outcome.Classify(error, false).ShouldBe(CommandOutcome.Unavailable);
    }

    [Fact]
    public void ACommandTimeout_IsTransientUnavailable()
    {
        var exception = new NpgsqlException("Exception while reading from stream", new TimeoutException("Timeout during reading attempt"));

        DatabaseRetry.IsTransient(Classify(exception)).ShouldBeTrue();
        DatabaseRetry.IsTransient(Classify(new TimeoutException())).ShouldBeTrue();
    }

    [Fact]
    public void ACancellationThatIsNotOurs_IsTransientUnavailable()
    {
        var error = Classify(new OperationCanceledException());

        error.ShouldBeOfType<Unavailable>();
        DatabaseRetry.IsTransient(error).ShouldBeTrue();
    }

    [Fact]
    public void AnNpgsqlExceptionMarkedTransient_ByAnInnerOne_IsTransientUnavailable()
    {
        var exception = new NpgsqlException("Exception while writing to stream",
            new NpgsqlException("inner", new IOException("broken pipe")));

        exception.IsTransient.ShouldBeTrue();
        DatabaseRetry.IsTransient(Classify(exception)).ShouldBeTrue();
    }

    [Fact]
    public void AnNpgsqlExceptionNotMarkedTransient_IsADefect()
    {
        var error = Classify(new NpgsqlException("The protocol was violated"));

        error.ShouldNotBeOfType<Unavailable>();
        error.Exception.Case.ShouldBeOfType<DatabaseException>().IsTransient.ShouldBeFalse();
    }

    [Fact]
    public void OurTokenCancelled_IsCancelled_WhateverTheException()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Classify(new OperationCanceledException(), run: source).ShouldBe(Errors.Cancelled);
        Classify(Postgres("57014"), run: source).ShouldBe(Errors.Cancelled);
        Classify(Postgres("23505"), run: source).ShouldBe(Errors.Cancelled);
    }

    [Fact]
    public void AnythingElse_IsADefect()
    {
        var error = Classify(new InvalidOperationException("Sequence contains no elements"));

        error.ShouldNotBeOfType<Unavailable>();
        Outcome.Classify(error, false).ShouldBe(CommandOutcome.Defect);
        var diagnosis = error.Exception.Case.ShouldBeOfType<DatabaseException>();
        diagnosis.InnerException.ShouldBeOfType<InvalidOperationException>();
        diagnosis.IsTransient.ShouldBeFalse();
    }

    [Theory]
    [InlineData("23505", "unique_violation")]
    [InlineData("40P01", "deadlock_detected")]
    [InlineData("ZZ999", "unknown_condition")]
    public void ConditionNames_ComeFromNpgsqlsCodes(string sqlState, string name) =>
        DatabaseErrors.ConditionName(sqlState).ShouldBe(name);

    [Fact]
    public void TheConditionNameTable_HasEveryNpgsqlCode()
    {
        var names = DatabaseErrors.BuildConditionNames();

        names.Count.ShouldBeGreaterThan(200);
        names["23505"].ShouldBe("unique_violation");
        names["08006"].ShouldBe("connection_failure");
    }

    [Theory]
    [InlineData("UniqueViolation", "unique_violation")]
    [InlineData("Warning", "warning")]
    [InlineData("SqlStatementNotYetComplete", "sql_statement_not_yet_complete")]
    public void SnakeCase_SplitsAtEachCapital(string pascal, string snake) =>
        DatabaseErrors.SnakeCase(pascal).ShouldBe(snake);
}

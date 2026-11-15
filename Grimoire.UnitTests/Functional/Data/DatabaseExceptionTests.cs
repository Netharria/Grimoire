// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Data;
using Grimoire.Functional.Runtime;
using Npgsql;

namespace Grimoire.UnitTests.Functional.Data;

/// <summary>What a database error log and alert say (spec 1.3, section 3.6.1).</summary>
public sealed class DatabaseExceptionTests
{
    private const string SecretValue = "SECRET-VALUE-7f3a";

    private static readonly Sql Insert = Sql.Write("Warn.InsertSin",
        "INSERT INTO \"Sins\" (\"Id\", \"Reason\") VALUES (@Id, @Reason)", new { Id = 5L, Reason = SecretValue });

    private static PostgresException UniqueViolation() =>
        new("duplicate key value violates unique constraint \"PK_Sins\"", "ERROR", "ERROR", "23505",
            detail: $"Key (\"Reason\")=({SecretValue}) already exists.", hint: "Try another key", position: 12,
            where: "SQL statement\nin function", schemaName: "public", tableName: "Sins",
            constraintName: "PK_Sins", routine: "_bt_check_unique");

    /// <summary>The exception the boundary logs and alerts with, for a defect or an outage alike.</summary>
    private static DatabaseException Diagnose(Exception exception, bool inTransaction = false) =>
        Boundary<GrimoireRuntime>.ExceptionOf(
                DatabaseErrors.Classify(exception, Insert, inTransaction, "db.example:5432", CancellationToken.None))
            .Case.ShouldBeOfType<DatabaseException>();

    [Fact]
    public void TheAlertsMessage_NamesTheStatementTheStateAndTheObjects()
    {
        var message = Diagnose(UniqueViolation(), true).Message;

        message.ShouldStartWith("Database statement Warn.InsertSin failed (write, never retried, in a transaction):");
        Diagnose(UniqueViolation()).Message
            .ShouldStartWith("Database statement Warn.InsertSin failed (write, never retried):");
        message.ShouldContain("23505 unique_violation: duplicate key value violates unique constraint \"PK_Sins\"");
        message.ShouldContain("schema \"public\", table \"Sins\", column —, constraint \"PK_Sins\", type —");
        message.ShouldNotContain("Hint");
        message.ShouldNotContain("@Reason");
    }

    [Fact]
    public void TheLog_AddsHintPositionContextRoutineAndParameterNames()
    {
        var diagnosis = Diagnose(UniqueViolation());

        diagnosis.Details.ShouldBe(
            "  Hint: Try another key · Position: 12 · Where: SQL statement | in function · Routine: _bt_check_unique\n"
            + "  Parameters: @Id, @Reason");
        diagnosis.ToString().ShouldStartWith($"{typeof(DatabaseException).FullName}: {diagnosis.Message}\n{diagnosis.Details}");
        diagnosis.ToString().ShouldContain(nameof(PostgresException));
    }

    [Fact]
    public void FieldsPostgresDidNotSet_AreDashes()
    {
        var bare = new PostgresException("syntax error", "ERROR", "ERROR", "42601");
        var diagnosis = Boundary<GrimoireRuntime>.ExceptionOf(DatabaseErrors.Classify(bare,
                Sql.Read("Test.Read", "SELEC 1"), false, "db:5432", CancellationToken.None))
            .Case.ShouldBeOfType<DatabaseException>();

        diagnosis.Message.ShouldBe("Database statement Test.Read failed (read, retried while transient):\n"
                                   + "  42601 syntax_error: syntax error\n"
                                   + "  Object: schema —, table —, column —, constraint —, type —");
        diagnosis.Details.ShouldBe("  Hint: — · Position: — · Where: — · Routine: —\n  Parameters: —");
    }

    [Fact]
    public void NeitherHoldsAParameterValueOrPostgresDetail()
    {
        var diagnosis = Diagnose(UniqueViolation());

        diagnosis.Message.ShouldNotContain(SecretValue);
        diagnosis.Details.ShouldNotContain(SecretValue);
        diagnosis.Message.ShouldNotContain("already exists");
    }

    [Fact]
    public void AConnectionFailure_NamesTheServerAndTheSocketError()
    {
        var socket = new SocketException((int)SocketError.ConnectionRefused);
        var message = Diagnose(new NpgsqlException("Failed to connect", socket)).Message;

        message.ShouldEndWith($"\n  NpgsqlException talking to db.example:5432: Failed to connect → {socket.Message}");
    }

    [Fact]
    public void TheOriginal_IsTheInnerException()
    {
        var original = UniqueViolation();

        Diagnose(original).InnerException.ShouldBeSameAs(original);
    }

    [Fact]
    public void TheServer_IsHostAndPort_NeverThePassword() =>
        NpgsqlDatabaseIO.Server("Host=db.example;Port=6543;Username=bot;Password=hunter2")
            .ShouldBe("db.example:6543");
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Grimoire.Data.Dapper;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Data;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using LanguageExt;
using Npgsql;
using static LanguageExt.Prelude;

namespace Grimoire.IntegrationTests.Database.Dapper;

/// <summary>The live database capability against Postgres (spec 1.3, sections 6 and 8).</summary>
[Collection("Test collection")]
public sealed class DapperDatabaseTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong Snowflake = 1_290_123_456_789_012_345;
    private static readonly DateTimeOffset Created = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private DatabaseTestEnv _env = null!;

    public ValueTask InitializeAsync()
    {
        this._env = new DatabaseTestEnv(factory.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await this._env.DisposeAsync();
        await factory.ResetDatabase();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record MessageRow(
        MessageId Id,
        GuildId GuildId,
        ChannelId ChannelId,
        UserId UserId,
        Option<MessageId> ReferencedMessageId,
        DateTimeOffset CreatedTimestamp);

    private static Sql InsertMessage(MessageId id, Option<MessageId> referenced = default) =>
        Sql.Write("Test.InsertMessage",
            """
            INSERT INTO "Messages" ("Id", "GuildId", "ChannelId", "UserId", "ReferencedMessageId", "CreatedTimestamp")
            VALUES (@Id, @GuildId, @ChannelId, @UserId, @Referenced, @Created)
            """,
            new
            {
                Id = id, GuildId = new GuildId(Snowflake + 1), ChannelId = new ChannelId(Snowflake + 2),
                UserId = new UserId(Snowflake + 3), Referenced = referenced, Created
            });

    private static readonly Sql SelectMessages = Sql.Read("Test.SelectMessages",
        """
        SELECT "Id", "GuildId", "ChannelId", "UserId", "ReferencedMessageId", "CreatedTimestamp"
        FROM "Messages" ORDER BY "Id"
        """);

    private static readonly Sql CountMessages = Sql.Read("Test.CountMessages", """SELECT count(*) FROM "Messages" """);

    private static DatabaseException Diagnosis(Error error) =>
        Boundary<GrimoireRuntime>.ExceptionOf(error).Case.ShouldBeOfType<DatabaseException>();

    [Fact]
    public async Task ARead_OpensAPooledConnection_AndReturnsARow() =>
        (await this._env.Run(Db<DatabaseRuntime>.QuerySingle<int>(Sql.Read("Test.One", "SELECT 1")), Token))
        .ShouldSucceed().ShouldBe(1);

    [Fact]
    public async Task Ids_RoundTripThroughNumeric_AndNoneIsNull()
    {
        var first = new MessageId(Snowflake);
        var second = new MessageId(Snowflake + 10);

        (await this._env.Run(Db<DatabaseRuntime>.Execute(InsertMessage(first)), Token)).ShouldSucceed().ShouldBe(1);
        (await this._env.Run(Db<DatabaseRuntime>.Execute(InsertMessage(second, first)), Token)).ShouldSucceed();

        var rows = (await this._env.Run(Db<DatabaseRuntime>.Query<MessageRow>(SelectMessages), Token)).ShouldSucceed();

        rows.ShouldBe([
            new MessageRow(first, new GuildId(Snowflake + 1), new ChannelId(Snowflake + 2),
                new UserId(Snowflake + 3), None, Created),
            new MessageRow(second, new GuildId(Snowflake + 1), new ChannelId(Snowflake + 2),
                new UserId(Snowflake + 3), Some(first), Created)
        ]);
    }

    [Fact]
    public async Task ABigintIdParameter_StillUsesTheNumericColumnsIndex()
    {
        var plan = await this._env.Run(Db<DatabaseRuntime>.Transaction(session =>
            from _ in session.Execute(Sql.Write("Test.NoSeqScan", "SET LOCAL enable_seqscan = off"))
            from lines in session.Query<string>(Sql.Read("Test.Explain",
                """EXPLAIN SELECT * FROM "Messages" WHERE "Id" = @Id""", new { Id = new MessageId(Snowflake) }))
            select string.Join('\n', lines)), Token);

        var text = plan.ShouldSucceed();
        text.ShouldContain("Index");
        text.ShouldNotContain("Seq Scan");
    }

    private sealed record TextRow(ModerationReason Reason, SinType Type, ReactionEmoji Emoji, Option<Nickname> Nickname);

    [Fact]
    public async Task TextValues_AndEnumsAsText_RoundTrip()
    {
        var emoji = ReactionEmoji.ParseFromDatabase("👍");

        var result = await this._env.Run(Db<DatabaseRuntime>.Transaction(session =>
            from _ in session.Execute(Sql.Write("Test.CreateTemp",
                "CREATE TEMP TABLE text_values (reason text, type text, emoji text, nickname text) ON COMMIT DROP"))
            from __ in session.Execute(Sql.Write("Test.InsertText",
                "INSERT INTO text_values VALUES (@Reason, @Type, @Emoji, @Nickname)",
                new
                {
                    Reason = ModerationReason.FromDatabase("spam"), Type = SinType.Mute.ToString(), Emoji = emoji,
                    Nickname = Option<Nickname>.None
                }))
            from row in session.QuerySingle<TextRow>(Sql.Read("Test.SelectText", "SELECT * FROM text_values"))
            select row), Token);

        var row = result.ShouldSucceed();
        row.Reason.Value.ShouldBe("spam");
        row.Type.ShouldBe(SinType.Mute);
        row.Emoji.ShouldBe(emoji);
        row.Nickname.ShouldBe(None);
    }

    /// <summary>
    ///     Why SQL takes <c>ToString()</c>: Dapper sends an enum as its number. An insert quietly stores "1" (Postgres
    ///     casts anything to text on assignment), and a comparison is refused.
    /// </summary>
    [Fact]
    public async Task AnEnumSentAsItsNumber_IsStoredAsTheNumber_AndRefusedInAComparison()
    {
        var stored = await this._env.Run(Db<DatabaseRuntime>.Transaction(session =>
            from _ in session.Execute(Sql.Write("Test.CreateTemp",
                "CREATE TEMP TABLE enum_values (type text) ON COMMIT DROP"))
            from __ in session.Execute(Sql.Write("Test.InsertEnum", "INSERT INTO enum_values VALUES (@Type)",
                new { Type = SinType.Mute }))
            from value in session.QuerySingle<string>(Sql.Read("Test.SelectEnum", "SELECT type FROM enum_values"))
            select value), Token);
        stored.ShouldSucceed().ShouldBe("1");

        var compared = await this._env.Run(Db<DatabaseRuntime>.Transaction(session =>
            from _ in session.Execute(Sql.Write("Test.CreateTemp",
                "CREATE TEMP TABLE enum_values (type text) ON COMMIT DROP"))
            from count in session.QuerySingle<long>(Sql.Read("Test.CompareEnum",
                "SELECT count(*) FROM enum_values WHERE type = @Type", new { Type = SinType.Mute }))
            select count), Token);

        var error = compared.ShouldFail();
        error.ShouldNotBeOfType<Unavailable>();
        Diagnosis(error).Message.ShouldContain("42883 undefined_function");
    }

    private sealed record TestDocument(int Version, bool Enabled = true, string? LogChannel = null);

    private sealed record DocumentRow(Jsonb<TestDocument> Document);

    private Task<Fin<Seq<DocumentRow>>> StoreAndRead(Sql insert) =>
        this._env.Run(Db<DatabaseRuntime>.Transaction(session =>
            from _ in session.Execute(Sql.Write("Test.CreateTemp",
                "CREATE TEMP TABLE documents (document jsonb) ON COMMIT DROP"))
            from __ in session.Execute(insert)
            from rows in session.Query<DocumentRow>(Sql.Read("Test.SelectDocuments", "SELECT document FROM documents"))
            select rows), Token);

    [Fact]
    public async Task AJsonbDocument_RoundTrips_IgnoringUnknownFields_AndDefaultingMissingOnes()
    {
        DapperTypeHandlers.RegisterDocument<TestDocument>(JsonSerializerOptions.Web);

        var written = await this.StoreAndRead(Sql.Write("Test.InsertDocument", "INSERT INTO documents VALUES (@Document)",
            new { Document = new Jsonb<TestDocument>(new TestDocument(2, false, "123")) }));
        written.ShouldSucceed().ShouldHaveSingleItem().Document.Value.ShouldBe(new TestDocument(2, false, "123"));

        var legacy = await this.StoreAndRead(Sql.Write("Test.InsertLegacyDocument",
            """INSERT INTO documents VALUES ('{"version": 1, "removedSetting": 5}')"""));
        legacy.ShouldSucceed().ShouldHaveSingleItem().Document.Value.ShouldBe(new TestDocument(1));
    }

    [Fact]
    public async Task AJsonbDocument_ThatDoesNotParse_IsADefect()
    {
        DapperTypeHandlers.RegisterDocument<TestDocument>(JsonSerializerOptions.Web);

        var result = await this.StoreAndRead(Sql.Write("Test.InsertBadDocument",
            """INSERT INTO documents VALUES ('{"version": "x"}')"""));

        var error = result.ShouldFail();
        error.ShouldNotBeOfType<Unavailable>();
        Outcome.Classify(error, false).ShouldBe(CommandOutcome.Defect);
    }

    [Fact]
    public async Task ATransaction_WhoseSecondStatementFails_LeavesNothing()
    {
        var result = await this._env.Run(Db<DatabaseRuntime>.Transaction(session =>
            from _ in session.Execute(InsertMessage(new MessageId(Snowflake)))
            from __ in session.QuerySingle<int>(Sql.Read("Test.DivideByZero", "SELECT 1 / 0"))
            select unit), Token);

        Diagnosis(result.ShouldFail()).Message.ShouldContain("22012 division_by_zero");
        (await this._env.Run(Db<DatabaseRuntime>.QuerySingle<long>(CountMessages), Token)).ShouldSucceed().ShouldBe(0);
    }

    [Fact]
    public async Task ATransaction_WhoseBodyFailsWithACase_RollsBack_AndKeepsTheCase()
    {
        var result = await this._env.Run(Db<DatabaseRuntime>.Transaction(session =>
            session.Execute(InsertMessage(new MessageId(Snowflake)))
                .Bind(_ => IO.fail<Unit>(new NotFound("That message is gone.")))), Token);

        result.ShouldFail().ShouldBeOfType<NotFound>().Message.ShouldBe("That message is gone.");
        (await this._env.Run(Db<DatabaseRuntime>.QuerySingle<long>(CountMessages), Token)).ShouldSucceed().ShouldBe(0);
    }

    [Fact]
    public async Task Fold_StreamsALargeResult()
    {
        var sum = await this._env.Run(Db<DatabaseRuntime>.Fold<long, long>(
            Sql.Read("Test.Series", "SELECT generate_series(1, 100000)::bigint"), 0, (total, row) => total + row), Token);

        sum.ShouldSucceed().ShouldBe(5_000_050_000L);
    }

    [Fact]
    public async Task AUniqueViolation_IsADefect_NamingTheTableAndConstraint_WithoutTheValue()
    {
        var id = new MessageId(Snowflake);
        (await this._env.Run(Db<DatabaseRuntime>.Execute(InsertMessage(id)), Token)).ShouldSucceed();

        var error = (await this._env.Run(Db<DatabaseRuntime>.Execute(InsertMessage(id)), Token)).ShouldFail();

        error.ShouldNotBeOfType<Unavailable>();
        var diagnosis = Diagnosis(error);
        diagnosis.Message.ShouldContain("Test.InsertMessage");
        diagnosis.Message.ShouldContain("23505 unique_violation");
        diagnosis.Message.ShouldContain("table \"Messages\"");
        diagnosis.Message.ShouldContain("constraint \"PK_Messages\"");
        diagnosis.ToString().ShouldNotContain(Snowflake.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task AStatementCancelledByTheToken_IsCancelled()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);
        source.CancelAfter(TimeSpan.FromMilliseconds(300));

        var result = await this._env.Run(
            Db<DatabaseRuntime>.QuerySingle<object>(Sql.Read("Test.Sleep", "SELECT pg_sleep(10)")), source.Token);

        result.ShouldFail().ShouldBe(Errors.Cancelled);
    }

    [Fact]
    public async Task AnUnreachableDatabase_IsUnavailable_AfterTheRetries()
    {
        await using var unreachable = new DatabaseTestEnv("Host=127.0.0.1;Port=1;Timeout=2;Username=x;Password=x");

        var error = (await unreachable.Run(Db<DatabaseRuntime>.QuerySingle<int>(Sql.Read("Test.One", "SELECT 1")),
            Token)).ShouldFail();

        error.ShouldBeOfType<Unavailable>();
        DatabaseRetry.IsTransient(error).ShouldBeTrue();
        Diagnosis(error).Message.ShouldContain("127.0.0.1:1");
        Diagnosis(error).Message.ShouldNotContain("Password");
    }

    [Fact]
    public async Task AReadThatWrites_FailsItsTest()
    {
        var result = await this._env.Run(Db<DatabaseRuntime>.Execute(Sql.Read("Test.WritesButSaysRead",
            """DELETE FROM "Messages" """)), Token);

        Diagnosis(result.ShouldFail()).Message.ShouldContain("25006 read_only_sql_transaction");
    }

    [Fact]
    public async Task QueryOptional_IsNone_AndQuerySingle_ADefect_WithNoRow()
    {
        var none = Sql.Read("Test.NoRow", "SELECT 1 WHERE false");

        (await this._env.Run(Db<DatabaseRuntime>.QueryOptional<int>(none), Token)).ShouldSucceed().ShouldBe(None);

        var error = (await this._env.Run(Db<DatabaseRuntime>.QuerySingle<int>(none), Token)).ShouldFail();
        error.ShouldNotBeOfType<Unavailable>();
        Outcome.Classify(error, false).ShouldBe(CommandOutcome.Defect);
    }
}

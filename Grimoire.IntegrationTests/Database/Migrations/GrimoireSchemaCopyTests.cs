// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Grimoire.IntegrationTests.Database.Migrations;

/// <summary>
///     Spec 2.3, section 8: the first deploy's path through the public schema. Each test seeds main's tables on a
///     database migrated to main's last migration, runs the rest of the chain as the deploy will, and checks the rows
///     that come out: pardons (Q8), the discriminators and bigint ids in one rewrite (Q9, P-9), the dropped GuildId
///     (Q11), the rename (TD-9) and the leaderboard view (Q12b, defect 23).
/// </summary>
[Collection("Test collection")]
public sealed class GrimoireSchemaCopyTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string MainsLastMigration = "20250705185227_SpamFilterOverrides";
    private const long Guild = 1;
    private const long Moderator = 9;
    private const long Alice = 100;
    private const long Bob = 101;
    private const long Zero = 102;

    /// <summary>Above 2^53, so a rounding cast anywhere in the chain would show.</summary>
    private const long Large = 1_234_567_890_123_456_789;

    private const long LargeMessage = 1_234_567_890_123_456_780;

    private readonly string _name = $"schema_{Guid.NewGuid():N}";

    private string ConnectionString
        => new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = this._name }.ToString();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await ExecuteAsync(database.ConnectionString, $"CREATE DATABASE {this._name}");
        await using var chain = MigrationChain.Context(this.ConnectionString);
        await chain.GetService<IMigrator>().MigrateAsync(MainsLastMigration, cancellationToken: Token);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteAsync(database.ConnectionString, $"DROP DATABASE IF EXISTS {this._name} WITH (FORCE)");
    }

    [Fact]
    public async Task XpHistory_KeepsEveryTotal_WithStringTypes_AndBigintIds()
    {
        await this.SeedAsync();
        await this.FinishTheChainAsync();

        (await this.RowsAsync("""
            SELECT "UserId", sum("Xp") FROM "XpHistory" GROUP BY "UserId" ORDER BY "UserId"
            """)).ShouldBe([[Alice, 14L], [Bob, 14L], [Zero, 0L], [Large, 3L]]);
        (await this.RowsAsync("""SELECT "Type", count(*) FROM "XpHistory" GROUP BY "Type" ORDER BY "Type" """))
            .ShouldBe([["Awarded", 1L], ["Earned", 4L], ["Migrated", 1L], ["Reclaimed", 2L]]);
        (await this.RowsAsync("""SELECT "AwarderId" FROM "XpHistory" WHERE "Type" = 'Reclaimed' AND "UserId" = 100"""))
            .ShouldBe([[Moderator]]);
    }

    [Fact]
    public async Task MessageHistory_GetsItsDiscriminators_AndLosesGuildId()
    {
        await this.SeedAsync();
        await this.FinishTheChainAsync();

        (await this.RowsAsync("""
            SELECT "MessageId", "Discriminator", "ModeratorId" FROM "MessageHistory" ORDER BY "TimeStamp"
            """)).ShouldBe(
        [
            [LargeMessage, "Created", null], [LargeMessage, "Edited", null], [LargeMessage, "Deleted", null],
            [LargeMessage + 1, "DeletedByModerator", Moderator]
        ]);
        (await this.RowsAsync("""
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'MessageHistory' AND column_name = 'GuildId'
            """)).ShouldBe([[0L]]);
    }

    /// <summary>Q8 and OQ-1: each pardon keeps its moderator and date; only a non-empty reason becomes history.</summary>
    [Fact]
    public async Task Pardons_KeepWhoAndWhen_AndAReasonOnlyWhenTheyHadOne()
    {
        await this.SeedAsync();
        await this.FinishTheChainAsync();

        (await this.RowsAsync("""SELECT "SinId", "ModeratorId", "SetAt" FROM "Pardons" ORDER BY "SinId" """))
            .ShouldBe([[1L, Moderator, PardonedAt], [2L, Moderator, PardonedAt.AddDays(1)]]);
        (await this.RowsAsync("""
            SELECT "SinId", "SetAt", "Reason", "ModeratorId" FROM "PardonReasonHistory" ORDER BY "SinId"
            """)).ShouldBe([[1L, PardonedAt, "sorry", Moderator]]);
    }

    [Fact]
    public async Task NicknameHistory_IsRenamed_WithItsRows()
        => await this.SeedThenAsync(async () =>
            (await this.RowsAsync("""
                SELECT "UserId", "DisplayName" FROM "ServerDisplayNameHistory" ORDER BY "UserId", "Timestamp"
                """)).ShouldBe([[Alice, "Old"], [Alice, "New"], [Bob, "Nick"], [Bob, null]]));

    /// <summary>Q12b, defect 23 and TD-9: no member at 0, ties by UserId, the latest names.</summary>
    [Fact]
    public async Task TheView_LeavesOutZero_BreaksTies_AndCarriesNames()
        => await this.SeedThenAsync(async () =>
            (await this.RowsAsync("""
                SELECT "UserId", "TotalXp", "Rank", "DisplayName", "Username" FROM leaderboard_view ORDER BY "Rank"
                """)).ShouldBe(
            [
                [Alice, 14L, 1L, "New", "alice"],
                [Bob, 14L, 2L, null, "bob"],
                [Large, 3L, 3L, null, null]
            ]));

    /// <summary>P-9 and D-20: every live id is bigint; the legacy tables stay, untouched; no key joins the two.</summary>
    [Fact]
    public async Task LiveIdsAreBigint_TheLegacyTablesStay_AndNoKeyJoinsThem()
        => await this.SeedThenAsync(async () =>
        {
            string[] tables =
            [
                .. GrimoireSchema.BigintColumns.Select(entry => entry.Table)
                    .Select(table => table == "NicknameHistory" ? "ServerDisplayNameHistory" : table),
                "SinReasonHistory", "CustomCommandUsages", "PardonReasonHistory"
            ];
            var live = string.Join(", ", tables.Select(table => $"'{table}'"));
            var liveRegclass = string.Join(", ", tables.Select(table => $"'\"{table}\"'"));
            (await this.RowsAsync($"""
                SELECT table_name, column_name FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name IN ({live}) AND data_type = 'numeric'
                """)).ShouldBeEmpty();
            (await this.RowsAsync("""
                SELECT data_type FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'Guilds' AND column_name = 'Id'
                """)).ShouldBe([["numeric"]]);
            (await this.RowsAsync("""SELECT count(*) FROM "Members" """)).ShouldBe([[5L]]);
            (await this.RowsAsync($"""
                SELECT conname FROM pg_constraint
                WHERE contype = 'f' AND connamespace = 'public'::regnamespace
                  AND conrelid::regclass::text IN ({liveRegclass}) AND confrelid::regclass::text NOT IN ({liveRegclass})
                """)).ShouldBeEmpty();
            (await this.RowsAsync("""
                SELECT conname FROM pg_constraint WHERE conname = 'FK_MessageHistory_Messages_MessageId'
                """)).ShouldHaveSingleItem();
        });

    [Theory]
    [InlineData("""UPDATE "XpHistory" SET "Type" = 7 WHERE "UserId" = 101""", "Type outside 0-3")]
    [InlineData("""UPDATE "MessageHistory" SET "Action" = 5 WHERE "Action" = 1""", "Action outside 0-2")]
    public async Task AnUnknownDiscriminator_StopsTheDeploy_BeforeItChangesAnything(string corrupt, string message)
    {
        await this.SeedAsync();
        await ExecuteAsync(this.ConnectionString, corrupt);

        (await Should.ThrowAsync<PostgresException>(this.FinishTheChainAsync())).MessageText.ShouldContain(message);
        (await this.RowsAsync("""
            SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%_GrimoireSchema'
            """)).ShouldBe([[0L]]);
        (await this.RowsAsync("""
            SELECT data_type FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'XpHistory' AND column_name = 'Type'
            """)).ShouldBe([["integer"]]);
    }

    private static readonly DateTimeOffset PardonedAt = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    private async Task SeedThenAsync(Func<Task> check)
    {
        await this.SeedAsync();
        await this.FinishTheChainAsync();
        await check();
    }

    /// <summary>Main's shapes: integer discriminators, numeric ids, pardons with a reason column, and the keys into
    /// the legacy tables, so every legacy parent row has to exist.</summary>
    private Task SeedAsync() => ExecuteAsync(this.ConnectionString, $"""
        INSERT INTO "Guilds" ("Id") VALUES ({Guild});
        INSERT INTO "Users" ("Id") VALUES ({Alice}), ({Bob}), ({Zero}), ({Moderator}), ({Large});
        INSERT INTO "Members" ("GuildId", "UserId")
        VALUES ({Guild}, {Alice}), ({Guild}, {Bob}), ({Guild}, {Zero}), ({Guild}, {Moderator}), ({Guild}, {Large});
        INSERT INTO "Channels" ("Id", "GuildId") VALUES (50, {Guild});

        INSERT INTO "XpHistory" ("UserId", "GuildId", "Xp", "TimeOut", "Type", "AwarderId") VALUES
            ({Alice}, {Guild}, 10, '2026-01-01T00:00:01Z', 0, NULL),
            ({Alice}, {Guild}, 5, '2026-01-01T00:00:02Z', 1, {Moderator}),
            ({Alice}, {Guild}, -3, '2026-01-01T00:00:03Z', 2, {Moderator}),
            ({Alice}, {Guild}, 2, '2026-01-01T00:00:04Z', 3, NULL),
            ({Alice}, {Guild}, 0, '2026-01-01T00:00:05Z', 4, NULL),
            ({Bob}, {Guild}, 14, '2026-01-01T00:00:01Z', 0, NULL),
            ({Zero}, {Guild}, 5, '2026-01-01T00:00:01Z', 0, NULL),
            ({Zero}, {Guild}, -5, '2026-01-01T00:00:02Z', 2, {Moderator}),
            ({Large}, {Guild}, 3, '2026-01-01T00:00:01Z', 0, NULL);

        INSERT INTO "Messages" ("Id", "UserId", "ChannelId", "GuildId", "CreatedTimestamp") VALUES
            ({LargeMessage}, {Alice}, 50, {Guild}, '2026-01-01T00:00:00Z'),
            ({LargeMessage + 1}, {Alice}, 50, {Guild}, '2026-01-01T00:00:00Z');
        INSERT INTO "MessageHistory" ("MessageId", "GuildId", "Action", "MessageContent", "DeletedByModeratorId", "TimeStamp") VALUES
            ({LargeMessage}, {Guild}, 0, 'hello', NULL, '2026-01-01T00:00:01Z'),
            ({LargeMessage}, {Guild}, 1, 'hello!', NULL, '2026-01-01T00:00:02Z'),
            ({LargeMessage}, {Guild}, 2, '', NULL, '2026-01-01T00:00:03Z'),
            ({LargeMessage + 1}, {Guild}, 2, '', {Moderator}, '2026-01-01T00:00:04Z');

        INSERT INTO "Sins" ("Id", "UserId", "ModeratorId", "GuildId", "Reason", "SinOn", "SinType") OVERRIDING SYSTEM VALUE VALUES
            (1, {Alice}, {Moderator}, {Guild}, 'spam', '2026-01-15T00:00:00Z', 0),
            (2, {Bob}, {Moderator}, {Guild}, 'rude', '2026-01-16T00:00:00Z', 0);
        INSERT INTO "Pardons" ("SinId", "ModeratorId", "GuildId", "PardonDate", "Reason") VALUES
            (1, {Moderator}, {Guild}, '2026-02-01T00:00:00Z', 'sorry'),
            (2, {Moderator}, {Guild}, '2026-02-02T00:00:00Z', '');

        INSERT INTO "NicknameHistory" ("UserId", "GuildId", "Nickname", "Timestamp") VALUES
            ({Alice}, {Guild}, 'Old', '2026-01-01T00:00:00Z'),
            ({Alice}, {Guild}, 'New', '2026-01-02T00:00:00Z'),
            ({Bob}, {Guild}, 'Nick', '2026-01-01T00:00:00Z'),
            ({Bob}, {Guild}, NULL, '2026-01-02T00:00:00Z');
        INSERT INTO "UsernameHistory" ("UserId", "Username", "Timestamp") VALUES
            ({Alice}, 'alice', '2026-01-01T00:00:00Z'),
            ({Bob}, 'bob', '2026-01-01T00:00:00Z');
        """);

    private async Task FinishTheChainAsync()
    {
        await using var chain = MigrationChain.Context(this.ConnectionString);
        await chain.Database.MigrateAsync(Token);
    }

    /// <summary>Rows as arrays, ids widened to long and times to UTC, so a test compares plain values.</summary>
    private async Task<List<object?[]>> RowsAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(this.ConnectionString);
        await connection.OpenAsync(Token);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Token);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(Token))
            rows.Add([.. Enumerable.Range(0, reader.FieldCount).Select(i => Normalise(reader.IsDBNull(i) ? null : reader.GetValue(i)))]);
        return rows;
    }

    private static object? Normalise(object? value) => value switch
    {
        int number => (long)number,
        decimal number => (long)number,
        DateTime time => new DateTimeOffset(time.ToUniversalTime(), TimeSpan.Zero),
        _ => value
    };

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

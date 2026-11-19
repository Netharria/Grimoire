// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Documents;
using Npgsql;

namespace Grimoire.IntegrationTests.Database.SettingsLayer;

/// <summary>
///     Spec 2.2, sections 6.1 and 6.2: the document write race, the state tables' check constraints and in-force
///     indexes, and a state change from a state it may not leave.
/// </summary>
[Collection("Settings collection")]
public sealed class SettingsSchemaTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private const string CheckViolation = "23514";
    private const string UniqueViolation = "23505";
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    // ── Documents ─────────────────────────────────────────────────────────────

    /// <summary>
    ///     Writers that start from the same version all land: each loser re-reads and re-applies, so no change is lost.
    ///     Three writers is the most the three tries are sure to cover.
    /// </summary>
    [Fact]
    public async Task ConcurrentWritesToOneDocument_AllLand()
    {
        var writes = new (LevelSettings Setting, int Value)[]
        {
            (LevelSettings.Amount, 10), (LevelSettings.Base, 20), (LevelSettings.Modifier, 30)
        };

        var results = await Task.WhenAll(writes.Select(write =>
            SettingsModuleFactory.Create(factory.ConnectionString)
                .SetLevelingSettings(_guildId, _modId, write.Setting, write.Value, Token)));

        results.ShouldAllBe(result => result.IsSucc);
        var settings = await SettingsModuleFactory.Create(factory.ConnectionString)
            .GetLevelingSettings(_guildId, Token).ShouldSucceed();
        settings.Amount.Value.ShouldBe(10);
        settings.Base.Value.ShouldBe(20);
        settings.Modifier.Value.ShouldBe(30);
        await using var db = factory.CreateDbContext();
        (await db.GuildSettings.Where(row => row.GuildId == _guildId).Select(row => row.Version).OrderBy(v => v)
            .ToListAsync(Token)).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task EachWrite_IsANewVersion_WithWhoAndWhen()
    {
        var settings = SettingsModuleFactory.Create(factory.ConnectionString);
        (await settings.SetModuleState(Module.Leveling, _guildId, _modId, true, Token)).ShouldBeSaved();
        (await settings.SetModuleState(Module.Leveling, _guildId, new ModeratorId(5), false, Token)).ShouldBeSaved();

        await using var db = factory.CreateDbContext();
        var rows = await db.GuildSettings.OrderBy(row => row.Version).ToListAsync(Token);
        rows.Select(row => (row.Version, row.SetBy)).ShouldBe([(1, _modId), (2, new ModeratorId(5))]);
        SettingsJson.Parse<ModuleStates>(rows[0].Value).Leveling.ShouldBeTrue();
        rows[1].Value.ShouldBe("{}");
    }

    [Fact]
    public async Task AModuleToggle_LeavesTheModulesSettingsAlone()
    {
        var settings = SettingsModuleFactory.Create(factory.ConnectionString);
        await settings.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 10, Token);
        await settings.SetModuleState(Module.Leveling, _guildId, _modId, true, Token);
        await settings.SetModuleState(Module.Leveling, _guildId, _modId, false, Token);
        await settings.SetModuleState(Module.Leveling, _guildId, _modId, true, Token);

        (await settings.GetLevelingSettings(_guildId, Token)).ShouldSucceed().Amount.Value.ShouldBe(10);
        await using var db = factory.CreateDbContext();
        (await db.GuildSettings.CountAsync(row => row.Document == SettingsDocument.Leveling, Token)).ShouldBe(1);
    }

    [Theory]
    [InlineData("""{"Leveling":true}""", 0)]
    [InlineData("[]", 1)]
    [InlineData("1", 1)]
    public async Task ADocumentRow_NeedsAVersionFromOne_AndAnObject(string value, int version)
        => (await Should.ThrowAsync<PostgresException>(() => this.ExecuteAsync($"""
                INSERT INTO "Settings"."GuildSettings" ("GuildId", "Document", "Version", "Value", "SetBy", "SetAt")
                VALUES (1, 'Modules', {version}, '{value}', 0, now());
                """))).SqlState.ShouldBe(CheckViolation);

    // ── State rows ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("'Active', NULL, NULL, NULL")]         // Active needs its start
    [InlineData("'Pending', now(), NULL, NULL")]       // Pending hasn't started
    [InlineData("'Expired', now(), NULL, NULL")]       // Expired needs its end
    [InlineData("'Expired', now(), now(), 5")]         // only Removed has a moderator
    [InlineData("'Removed', now(), now(), NULL")]      // Removed needs its moderator
    [InlineData("'Lifted', now(), NULL, NULL")]        // not a state
    public async Task AMuteRow_HoldsExactlyWhatItsStateNeeds(string state)
        => (await Should.ThrowAsync<PostgresException>(() => this.ExecuteAsync($"""
                INSERT INTO "Settings"."Mutes" ("GuildId", "UserId", "SinId", "ModeratorId", "SetAt", "EndTime",
                    "State", "ActivatedAt", "EndedAt", "RemovedBy")
                VALUES (1, 2, 3, 9, now(), now() + interval '1 hour', {state});
                """))).SqlState.ShouldBe(CheckViolation);

    [Fact]
    public async Task AMember_HasAtMostOneMuteInForce_ButAnyNumberEnded()
    {
        await this.ExecuteAsync("""
            INSERT INTO "Settings"."Mutes" ("GuildId", "UserId", "SinId", "ModeratorId", "SetAt", "EndTime", "State",
                "ActivatedAt", "EndedAt", "RemovedBy")
            VALUES (1, 2, 3, 9, now(), now(), 'Removed', now(), now(), 9),
                   (1, 2, 4, 9, now(), now(), 'Expired', now(), now(), NULL),
                   (1, 2, 5, 9, now(), now() + interval '1 hour', 'Active', now(), NULL, NULL);
            """);

        (await Should.ThrowAsync<PostgresException>(() => this.ExecuteAsync("""
            INSERT INTO "Settings"."Mutes" ("GuildId", "UserId", "SinId", "ModeratorId", "SetAt", "EndTime", "State")
            VALUES (1, 2, 6, 9, now(), now() + interval '1 hour', 'Pending');
            """))).SqlState.ShouldBe(UniqueViolation);
    }

    [Theory]
    [InlineData("ChannelLocks", ", \"PreviouslyAllowed\", \"PreviouslyDenied\"", ", 0, 0")]
    [InlineData("ThreadLocks", "", "")]
    public async Task AChannel_HasAtMostOneLockInForce(string table, string columns, string values)
    {
        var insert = $"""
            INSERT INTO "Settings"."{table}" ("GuildId", "ChannelId", "ModeratorId", "SetAt", "EndTime", "State",
                "ActivatedAt"{columns})
            VALUES (1, 2, 9, now(), now() + interval '1 hour', 'Active', now(){values});
            """;
        await this.ExecuteAsync(insert);

        (await Should.ThrowAsync<PostgresException>(() => this.ExecuteAsync(insert))).SqlState.ShouldBe(UniqueViolation);
    }

    [Fact]
    public async Task EachMute_ComesFromItsOwnSin()
    {
        var settings = SettingsModuleFactory.Create(factory.ConnectionString);
        await settings.AddMute(new UserId(2), _guildId, _modId, new SinId(7), DateTimeOffset.UtcNow.AddHours(1), null,
            Token).ShouldSucceed();

        (await Should.ThrowAsync<PostgresException>(() => this.ExecuteAsync("""
            INSERT INTO "Settings"."Mutes" ("GuildId", "UserId", "SinId", "ModeratorId", "SetAt", "EndTime", "State")
            VALUES (1, 3, 7, 9, now(), now() + interval '1 hour', 'Pending');
            """))).SqlState.ShouldBe(UniqueViolation);
    }

    /// <summary>TD-1: an ended mute can't end again. The UPDATE matches no row, which is NotFound, not an exception.</summary>
    [Fact]
    public async Task AnEndedMute_CannotBeExpiredOrRemovedAgain()
    {
        var settings = SettingsModuleFactory.Create(factory.ConnectionString);
        var mute = await settings.AddMute(new UserId(2), _guildId, _modId, new SinId(7),
            DateTimeOffset.UtcNow.AddHours(1), null, Token).ShouldSucceed();
        (await settings.RemoveMute(mute.UserId, _guildId, _modId, Token)).ShouldSucceed();

        (await settings.ExpireMute(mute, Token)).ShouldFail().ShouldBeOfType<NotFound>();
        (await settings.RemoveMute(mute.UserId, _guildId, _modId, Token)).ShouldFail().ShouldBeOfType<NotFound>();
        await using var db = factory.CreateDbContext();
        (await db.Mutes.SingleAsync(Token)).State.ShouldBe("Removed");
    }

    [Fact]
    public async Task AnExpiredLock_IsExpired_AndNoLongerLocks()
    {
        var settings = SettingsModuleFactory.Create(factory.ConnectionString);
        var channelLock = await settings.AddChannelLock(NewChannelLock.Create(_modId, null, new ChannelId(2), _guildId,
            DateTimeOffset.UtcNow, new PreviouslyAllowedPermissions(0), new PreviouslyDeniedPermissions(0),
            DateTimeOffset.UtcNow.AddHours(1)).ShouldSucceed(), Token).ShouldSucceed();

        var expired = await settings.ExpireChannelLock(channelLock, Token).ShouldSucceed();

        expired.State.ShouldBeOfType<ItemState.Expired>();
        (await settings.IsChannelLocked(new ChannelId(2), _guildId, Token)).ShouldSucceed().ShouldBeFalse();
        (await settings.ExpireChannelLock(channelLock, Token)).ShouldFail().ShouldBeOfType<NotFound>();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync(Token);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Token);
    }
}

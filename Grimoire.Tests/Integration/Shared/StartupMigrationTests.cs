// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Grimoire.Settings;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Startup and migrations (backlog A-38). On start the bot applies any pending migrations to both the main and
///     the settings schema, then connects. Each test gets a fresh database in the shared container, so the shared
///     database the other tests use is never touched.
/// </summary>
[Collection("Test collection")]
public sealed class StartupMigrationTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string MigrationLog = "Applied pending migrations";
    private const string SettingsMigrationLog = "Applied pending settings migrations";

    private readonly string _name = $"startup_{Guid.NewGuid():N}";

    private string ConnectionString => new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = this._name }.ToString();

    public async ValueTask InitializeAsync() => await this.ExecuteOnServerAsync($"CREATE DATABASE {this._name}");

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await this.ExecuteOnServerAsync($"DROP DATABASE IF EXISTS {this._name} WITH (FORCE)");
    }

    /// <remarks>Cases: XC-20.</remarks>
    [Fact]
    public async Task OnAnEmptyDatabase_AllMigrationsAreApplied_AndTheBotWorks()
    {
        await using var bot = await GrimoireTestBot.StartAsync(database, connectionString: this.ConnectionString);

        await using var main = this.MainContext();
        (await main.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        await using var settings = this.SettingsContext();
        (await settings.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        bot.Logs.Events.ShouldContain(e => e.RenderMessage(null).StartsWith(MigrationLog, StringComparison.Ordinal));
        (await bot.RunSlashCommandAndWaitAsync("modules", bot.Guild.Admin, SlashOption.Subcommand("view"))).Embed.ShouldNotBeNull();
        // EF reads the migration history before the table exists on a fresh database, and logs that as an error.
        bot.Logs.Errors.Where(error => !error.Contains("__EFMigrationsHistory", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    /// <summary>A database one migration behind on each schema is brought up to date.</summary>
    /// <remarks>Cases: XC-21.</remarks>
    [Fact]
    public async Task OnADatabaseOneMigrationBehind_TheLastMigrationIsApplied()
    {
        await using (var main = this.MainContext())
            await main.GetService<IMigrator>().MigrateAsync(main.Database.GetMigrations().SkipLast(1).Last(),
                cancellationToken: TestContext.Current.CancellationToken);
        await using (var settings = this.SettingsContext())
            await settings.GetService<IMigrator>().MigrateAsync(settings.Database.GetMigrations().SkipLast(1).Last(),
                cancellationToken: TestContext.Current.CancellationToken);

        await using var bot = await GrimoireTestBot.StartAsync(database, connectionString: this.ConnectionString);

        // Both contexts share __EFMigrationsHistory, so the newest applied row can belong to either one.
        await using var mainAfter = this.MainContext();
        (await mainAfter.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        await using var settingsAfter = this.SettingsContext();
        (await settingsAfter.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        bot.Logs.Events.ShouldContain(e => e.RenderMessage(null).StartsWith(SettingsMigrationLog, StringComparison.Ordinal));
        bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     Review S-13: <c>AddClosedAtToLocksAndMutes</c> closes every lock and mute that a later event for the same
    ///     channel or member ended or replaced, at that event's time, and leaves the ones still in force open.
    /// </summary>
    [Fact]
    public async Task AddClosedAt_ClosesEverythingALaterEventEnded_AndLeavesTheRestOpen()
    {
        var token = TestContext.Current.CancellationToken;
        await using (var settings = this.SettingsContext())
        {
            var migrations = settings.Database.GetMigrations().ToList();
            var target = migrations.Single(name => name.EndsWith("_AddClosedAtToLocksAndMutes", StringComparison.Ordinal));
            await settings.GetService<IMigrator>().MigrateAsync(migrations[migrations.IndexOf(target) - 1],
                cancellationToken: token);
            // Channel 1: locked, unlocked, locked again (open). Channel 2: locked, replaced by a newer lock (open).
            // Member 5: muted then unmuted. Member 6: muted (open). Guild 2 has its own lock on channel 1 (open).
            await settings.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Settings"."ChannelLocks" ("ChannelId", "GuildId", "SetAt", "EventType", "ModeratorId", "EndTime", "PreviouslyAllowed", "PreviouslyDenied") VALUES
                    (1, 1, '2026-01-01T00:00:00Z', 'Locked',   9, '2026-01-01T01:00:00Z', 0, 0),
                    (1, 1, '2026-01-01T00:30:00Z', 'Unlocked', 9, NULL, NULL, NULL),
                    (1, 1, '2026-01-02T00:00:00Z', 'Locked',   9, '2026-01-02T01:00:00Z', 0, 0),
                    (2, 1, '2026-01-01T00:00:00Z', 'Locked',   9, '2026-01-01T01:00:00Z', 0, 0),
                    (2, 1, '2026-01-01T00:10:00Z', 'Locked',   9, '2026-01-01T02:00:00Z', 0, 0),
                    (1, 2, '2026-01-01T00:00:00Z', 'Locked',   9, '2026-01-01T01:00:00Z', 0, 0);
                INSERT INTO "Settings"."ThreadLocks" ("ChannelId", "GuildId", "SetAt", "EventType", "ModeratorId", "EndTime") VALUES
                    (3, 1, '2026-01-01T00:00:00Z', 'Locked',   9, '2026-01-01T01:00:00Z'),
                    (3, 1, '2026-01-01T00:20:00Z', 'Unlocked', 9, NULL);
                INSERT INTO "Settings"."Mutes" ("UserId", "GuildId", "SetAt", "EventType", "ModeratorId", "EndTime", "SinId") VALUES
                    (5, 1, '2026-01-01T00:00:00Z', 'Added',   9, '2026-01-01T01:00:00Z', 1),
                    (5, 1, '2026-01-01T00:40:00Z', 'Removed', 9, NULL, NULL),
                    (6, 1, '2026-01-01T00:00:00Z', 'Added',   9, '2026-01-01T01:00:00Z', 2);
                """, token);
            await settings.GetService<IMigrator>().MigrateAsync(target, cancellationToken: token);
        }

        await using var after = this.SettingsContext();
        static DateTimeOffset At(string time) => DateTimeOffset.Parse($"2026-01-{time}Z", CultureInfo.InvariantCulture);
        (await after.ChannelLocks.OfType<ChannelLocked>().OrderBy(x => x.GuildId).ThenBy(x => x.ChannelId).ThenBy(x => x.SetAt)
                .Select(x => x.ClosedAt).ToListAsync(token))
            .ShouldBe([At("01T00:30:00"), null, At("01T00:10:00"), null, null]);
        (await after.ThreadLocks.OfType<ThreadLocked>().Select(x => x.ClosedAt).SingleAsync(token))
            .ShouldBe(At("01T00:20:00"));
        (await after.Mutes.OfType<MuteAdded>().OrderBy(x => x.UserId).Select(x => x.ClosedAt).ToListAsync(token))
            .ShouldBe([At("01T00:40:00"), null]);
    }

    /// <summary>An up-to-date database starts without migrating.</summary>
    /// <remarks>Cases: XC-01.</remarks>
    [Fact]
    public async Task OnAnUpToDateDatabase_NothingIsMigrated()
    {
        await using (var first = await GrimoireTestBot.StartAsync(database, connectionString: this.ConnectionString))
        {
        }

        await using var bot = await GrimoireTestBot.StartAsync(database, connectionString: this.ConnectionString);

        bot.Logs.Events.ShouldNotContain(e => e.RenderMessage(null).StartsWith("Applied pending", StringComparison.Ordinal));
    }

    private GrimoireDbContext MainContext()
        => new(new DbContextOptionsBuilder<GrimoireDbContext>().UseNpgsql(this.ConnectionString).Options);

    private SettingsDbContext SettingsContext()
        => new(new DbContextOptionsBuilder<SettingsDbContext>().UseNpgsql(this.ConnectionString).Options);

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

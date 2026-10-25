// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings;
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

    private readonly GrimoireCoreFactory _database = database;
    private readonly string _name = $"startup_{Guid.NewGuid():N}";

    private string ConnectionString => new NpgsqlConnectionStringBuilder(this._database.ConnectionString) { Database = this._name }.ToString();

    public async ValueTask InitializeAsync() => await this.ExecuteOnServerAsync($"CREATE DATABASE {this._name}");

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await this.ExecuteOnServerAsync($"DROP DATABASE IF EXISTS {this._name} WITH (FORCE)");
    }

    [Fact]
    public async Task OnAnEmptyDatabase_AllMigrationsAreApplied_AndTheBotWorks()
    {
        await using var bot = await GrimoireTestBot.StartAsync(this._database, connectionString: this.ConnectionString);

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
    [Fact]
    public async Task OnADatabaseOneMigrationBehind_TheLastMigrationIsApplied()
    {
        await using (var main = this.MainContext())
            await main.GetService<IMigrator>().MigrateAsync(main.Database.GetMigrations().SkipLast(1).Last(),
                cancellationToken: TestContext.Current.CancellationToken);
        await using (var settings = this.SettingsContext())
            await settings.GetService<IMigrator>().MigrateAsync(settings.Database.GetMigrations().SkipLast(1).Last(),
                cancellationToken: TestContext.Current.CancellationToken);

        await using var bot = await GrimoireTestBot.StartAsync(this._database, connectionString: this.ConnectionString);

        await using var mainAfter = this.MainContext();
        (await mainAfter.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).Last()
            .ShouldBe(mainAfter.Database.GetMigrations().Last());
        await using var settingsAfter = this.SettingsContext();
        (await settingsAfter.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        bot.Logs.Events.ShouldContain(e => e.RenderMessage(null).StartsWith(SettingsMigrationLog, StringComparison.Ordinal));
        bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>An up-to-date database starts without migrating.</summary>
    [Fact]
    public async Task OnAnUpToDateDatabase_NothingIsMigrated()
    {
        await using (var first = await GrimoireTestBot.StartAsync(this._database, connectionString: this.ConnectionString))
        {
        }

        await using var bot = await GrimoireTestBot.StartAsync(this._database, connectionString: this.ConnectionString);

        bot.Logs.Events.ShouldNotContain(e => e.RenderMessage(null).StartsWith("Applied pending", StringComparison.Ordinal));
    }

    private GrimoireDbContext MainContext()
        => new(new DbContextOptionsBuilder<GrimoireDbContext>().UseNpgsql(this.ConnectionString).Options);

    private SettingsDbContext SettingsContext()
        => new(new DbContextOptionsBuilder<SettingsDbContext>().UseNpgsql(this.ConnectionString).Options);

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(this._database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

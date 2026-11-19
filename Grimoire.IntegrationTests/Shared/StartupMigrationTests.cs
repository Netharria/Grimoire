// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Grimoire.IntegrationTests.Shared;

/// <summary>
///     Startup and migrations (backlog A-38). On start the bot applies any pending migrations of the one chain that
///     covers both the main and the settings schema (spec 2.0), then connects. Each test gets a fresh database in the shared container, so the shared
///     database the other tests use is never touched.
/// </summary>
[Collection("Test collection")]
public sealed class StartupMigrationTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string MigrationLog = "Applied pending migrations";

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

        await using var chain = MigrationChain.Context(this.ConnectionString);
        (await chain.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        bot.Logs.Events.Count(e => e.RenderMessage(null).StartsWith(MigrationLog, StringComparison.Ordinal)).ShouldBe(1);
        (await bot.RunSlashCommandAndWaitAsync("modules", bot.Guild.Admin, SlashOption.Subcommand("view"))).Embed.ShouldNotBeNull();
        // EF reads the migration history before the table exists on a fresh database, and logs that as an error.
        bot.Logs.Errors.Where(error => !error.Contains("__EFMigrationsHistory", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    /// <summary>A database one migration behind on the chain is brought up to date.</summary>
    /// <remarks>Cases: XC-21.</remarks>
    [Fact]
    public async Task OnADatabaseOneMigrationBehind_TheLastMigrationIsApplied()
    {
        await using (var chain = MigrationChain.Context(this.ConnectionString))
            await chain.GetService<IMigrator>().MigrateAsync(chain.Database.GetMigrations().SkipLast(1).Last(),
                cancellationToken: TestContext.Current.CancellationToken);

        await using var bot = await GrimoireTestBot.StartAsync(database, connectionString: this.ConnectionString);

        await using var after = MigrationChain.Context(this.ConnectionString);
        (await after.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        bot.Logs.Events.ShouldContain(e => e.RenderMessage(null).StartsWith(MigrationLog, StringComparison.Ordinal));
        bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     Spec 2.0: in the one chain, Settings <c>Initial</c> (which copies the legacy settings out of
    ///     <c>public</c>) runs straight after <c>ModelRedesign</c>, and so before every later migration of either
    ///     schema. 2.0 proved that this order copies the same data as the old one (the whole Grimoire chain first) on a
    ///     copy of production. A new id that moved <c>Initial</c> needs that check again.
    /// </summary>
    [Fact]
    public void SettingsInitial_RunsStraightAfterModelRedesign()
    {
        using var chain = MigrationChain.Context(this.ConnectionString);
        var migrations = chain.Database.GetMigrations().ToList();

        var initial = migrations.FindIndex(name => name.EndsWith("_Initial", StringComparison.Ordinal));
        var modelRedesign = migrations.FindIndex(name => name.EndsWith("_ModelRedesign", StringComparison.Ordinal));

        initial.ShouldBe(modelRedesign + 1);
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

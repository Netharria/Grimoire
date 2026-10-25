// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data.Common;
using EntityFramework.Exceptions.PostgreSQL;
using Grimoire.Settings;
using JetBrains.Annotations;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Grimoire.Tests;

[UsedImplicitly]
public sealed class GrimoireCoreFactory : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer =
        new PostgreSqlBuilder("postgres:18-alpine")
            .Build();

    private DbConnection _dbConnection = null!;
    private Respawner _respawner = null!;

    internal string ConnectionString => this._postgreSqlContainer.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await this._postgreSqlContainer.StartAsync();

        await using var dbContext = new GrimoireDbContext(
            new DbContextOptionsBuilder<GrimoireDbContext>()
                .UseNpgsql(this._postgreSqlContainer.GetConnectionString())
                .Options);

        await dbContext.Database.MigrateAsync();

        // Integration tests run the whole bot, which also reads guild settings, so the settings schema must exist
        // before Respawn takes its table snapshot or it would never be reset between tests.
        await using var settingsDbContext = new SettingsDbContext(
            new DbContextOptionsBuilder<SettingsDbContext>()
                .UseNpgsql(this._postgreSqlContainer.GetConnectionString())
                .Options);
        await settingsDbContext.Database.MigrateAsync();

        this._dbConnection = new NpgsqlConnection(this._postgreSqlContainer.GetConnectionString());
        await this._dbConnection.OpenAsync();
        this._respawner = await Respawner.CreateAsync(this._dbConnection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                // The bot checks for pending migrations at startup; wiping the history would make it re-run them.
                TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")]
            });
    }

    public ValueTask DisposeAsync() => this._postgreSqlContainer.DisposeAsync();

    public Task ResetDatabase() => this._respawner.ResetAsync(this._dbConnection);

    public GrimoireDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(this.ConnectionString)
            .LogTo(Console.WriteLine)
            .UseExceptionProcessor()
            .Options);

    public SettingsDbContext CreateSettingsDbContext()
        => new(new DbContextOptionsBuilder<SettingsDbContext>()
            .UseNpgsql(this.ConnectionString)
            .Options);

    /// <summary>For constructing production classes that take an <see cref="IDbContextFactory{TContext}"/> dependency directly.</summary>
    public IDbContextFactory<GrimoireDbContext> CreateDbContextFactory() => new TestDbContextFactory(this);

    private sealed class TestDbContextFactory(GrimoireCoreFactory factory) : IDbContextFactory<GrimoireDbContext>
    {
        public GrimoireDbContext CreateDbContext() => factory.CreateDbContext();

        public Task<GrimoireDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(factory.CreateDbContext());
    }
}

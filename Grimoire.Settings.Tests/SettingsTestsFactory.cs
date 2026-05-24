// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data.Common;
using JetBrains.Annotations;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Grimoire.Settings.Tests;

[UsedImplicitly]
public sealed class SettingsTestsFactory : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer =
        new PostgreSqlBuilder("postgres:18-alpine")
            .Build();

    private DbConnection _dbConnection = null!;
    private Respawner _respawner = null!;

    public string ConnectionString => this._postgreSqlContainer.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await this._postgreSqlContainer.StartAsync();

        await using var dbContext = new SettingsDbContext(
            new DbContextOptionsBuilder<SettingsDbContext>()
                .UseNpgsql(this._postgreSqlContainer.GetConnectionString())
                .Options);

        await dbContext.Database.MigrateAsync();

        this._dbConnection = new NpgsqlConnection(this._postgreSqlContainer.GetConnectionString());
        await this._dbConnection.OpenAsync();

        this._respawner = await Respawner.CreateAsync(this._dbConnection,
            new RespawnerOptions { SchemasToInclude = ["Settings"], DbAdapter = DbAdapter.Postgres });
    }

    public ValueTask DisposeAsync() => this._postgreSqlContainer.DisposeAsync();

    public Task ResetDatabase() => this._respawner.ResetAsync(this._dbConnection);

    public SettingsDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<SettingsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);
}

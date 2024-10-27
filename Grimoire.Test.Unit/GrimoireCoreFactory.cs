// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace Grimoire.Test.Unit;

public sealed class GrimoireCoreFactory : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer =
        new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .Build();

    private DbConnection _dbConnection = default!;
    private Respawner _respawner = default!;


    public string ConnectionString => this._postgreSqlContainer.GetConnectionString();


    public async Task InitializeAsync()
    {
        await this._postgreSqlContainer.StartAsync();
        this._dbConnection = new NpgsqlConnection(this._postgreSqlContainer.GetConnectionString());

        var dbContext = new GrimoireDbContext(
            new DbContextOptionsBuilder<GrimoireDbContext>()
                .UseNpgsql(this._postgreSqlContainer.GetConnectionString())
                .Options);

        await dbContext.Database.MigrateAsync();

        await this._dbConnection.OpenAsync();
        this._respawner = await Respawner.CreateAsync(this._dbConnection,
            new RespawnerOptions { DbAdapter = DbAdapter.Postgres });
    }

    public Task DisposeAsync() => this._postgreSqlContainer.DisposeAsync().AsTask();

    public async Task ResetDatabase() => await this._respawner.ResetAsync(this._dbConnection);
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using EntityFramework.Exceptions.PostgreSQL;
using FluentAssertions;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class IdentifiableDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong Guild1 = 1;
    private const ulong Guild2 = 2;

    private readonly GrimoireDbContext _dbContext = new GrimoireDbContext(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .UseExceptionProcessor()
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = Guild1 });
        await this._dbContext.AddAsync(new Guild { Id = Guild2 });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhereIdsAre_WhenProvidedValidIds_ReturnsResultAsync()
    {
        var result = await this._dbContext.Guilds.WhereIdsAre(new[] { Guild1 }).ToArrayAsync();

        result.Should().HaveCount(1);
        result.Should().AllSatisfy(x => x.Id.Should().Be(Guild1));
    }

    [Fact]
    public async Task WhereIdIs_WhenProvidedValidId_ReturnsResultAsync()
    {
        var result = await this._dbContext.Guilds.WhereIdIs(Guild2).FirstOrDefaultAsync();

        result.Should().NotBeNull();
        result!.Id.Should().Be(Guild2);
    }
}

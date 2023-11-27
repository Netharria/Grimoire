// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public class IIdentifiableDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_1 = 1;
    private const ulong GUILD_2 = 2;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_1 });
        await this._dbContext.AddAsync(new Guild { Id = GUILD_2 });
        await this._dbContext.SaveChangesAsync();
    }
    public Task DisposeAsync() => this._resetDatabase();
    [Fact]
    public async Task WhereIdsAre_WhenProvidedValidIds_ReturnsResultAsync()
    {
        var result = await this._dbContext.Guilds.WhereIdsAre(new ulong[]{ GUILD_1 }).ToArrayAsync();

        result.Should().HaveCount(1);
        result.Should().AllSatisfy(x => x.Id.Should().Be(GUILD_1));
    }

    [Fact]
    public async Task WhereIdIs_WhenProvidedValidId_ReturnsResultAsync()
    {
        var result = await this._dbContext.Guilds.WhereIdIs(GUILD_2).FirstOrDefaultAsync();

        result.Should().NotBeNull();
        result!.Id.Should().Be(GUILD_2);
    }
}

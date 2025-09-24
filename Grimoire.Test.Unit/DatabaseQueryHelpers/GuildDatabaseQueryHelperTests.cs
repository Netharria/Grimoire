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
using Grimoire.Domain.Obsolete;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class GuildDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong Guild1 = 1;
    private const ulong Guild2 = 2;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .UseExceptionProcessor()
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(
            new Guild
            {
                Id = Guild1,
            });
        await this._dbContext.AddAsync(
            new Guild
            {
                Id = Guild2,
            });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenGuildsAreNotInDatabase_AddThemAsync()
    {
        ulong[] guildsToAdd = [Guild1, Guild2, 3];
        var result = await this._dbContext.Guilds.AddMissingGuildsAsync(guildsToAdd);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        var guilds = await this._dbContext.Guilds
            .ToListAsync();
        guilds.Should().HaveCount(3)
            .And.AllSatisfy(x =>
            {
                x.Id.Should().BeOneOf(Guild1, Guild2, 3);
            });
    }

    [Fact]
    public async Task WhenNoGuildsAreAdded_ReturnsFalse()
    {
        // Arrange
        ulong[] guildsToAdd = []; // No members to add

        // Act
        var result = await this._dbContext.Guilds.AddMissingGuildsAsync(guildsToAdd);

        // Assert
        result.Should().BeFalse();
    }
}

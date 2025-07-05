// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EntityFramework.Exceptions.PostgreSQL;
using FluentAssertions;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Domain;
using Grimoire.Features.Shared.SharedDtos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class RoleDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const long GuildId = 1;
    private const long Role1 = 1;
    private const long Role2 = 2;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .UseExceptionProcessor()
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GuildId });
        await this._dbContext.AddAsync(new Role { Id = Role1, GuildId = GuildId });
        await this._dbContext.AddAsync(new Role { Id = Role2, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenRolesAreNotInDatabase_AddThemAsync()
    {
        var rolesToAdd = new List<RoleDto>
        {
            new() { Id = Role1, GuildId = GuildId },
            new() { Id = Role2, GuildId = GuildId },
            new() { Id = 4, GuildId = GuildId },
            new() { Id = 5, GuildId = GuildId }
        };
        var result = await this._dbContext.Roles.AddMissingRolesAsync(rolesToAdd);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        this._dbContext.Roles.Should().HaveCount(4);
    }

    [Fact]
    public async Task WhenNoRolesAreAdded_ReturnsFalse()
    {
        // Arrange
        var rolesToAdd = Array.Empty<RoleDto>(); // No members to add

        // Act
        var result = await this._dbContext.Roles.AddMissingRolesAsync(rolesToAdd);

        // Assert
        result.Should().BeFalse();
    }
}

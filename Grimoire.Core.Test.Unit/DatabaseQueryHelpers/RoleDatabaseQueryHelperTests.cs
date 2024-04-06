// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.Shared.SharedDtos;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class RoleDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const long GUILD_ID = 1;
    private const long ROLE_1 = 1;
    private const long ROLE_2 = 2;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_1, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_2, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenRolesAreNotInDatabase_AddThemAsync()
    {
        var rolesToAdd = new List<RoleDto>
        {
            new() { Id = ROLE_1, GuildId = GUILD_ID },
            new() { Id = ROLE_2, GuildId = GUILD_ID },
            new() { Id = 4, GuildId = GUILD_ID },
            new() { Id = 5, GuildId = GUILD_ID }
        };
        var result = await this._dbContext.Roles.AddMissingRolesAsync(rolesToAdd, default);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        this._dbContext.Roles.Should().HaveCount(4);
    }
}

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
public class GuildDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
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
    public async Task WhenGuildsAreNotInDatabase_AddThemAsync()
    {
        var guildsToAdd = new List<GuildDto>
        {
            new() { Id = GUILD_1 },
            new() { Id = GUILD_2 },
            new() { Id = 3 }
        };
        var result = await this._dbContext.Guilds.AddMissingGuildsAsync(guildsToAdd, default);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        this._dbContext.Guilds.Should().HaveCount(3);
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.Shared.SharedDtos;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class UserDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const long USER_1 = 1;
    private const long USER_2 = 2;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new User { Id = USER_1 });
        await this._dbContext.AddAsync(new UsernameHistory
        {
            UserId = USER_1,
            Username = "User1"
        });
        await this._dbContext.AddAsync(new User { Id = USER_2 });
        await this._dbContext.AddAsync(new UsernameHistory
        {
            UserId = USER_2,
            Username = "User2"
        });
        await this._dbContext.SaveChangesAsync();
    }
    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenUsersAreNotInDatabase_AddThemAsync()
    {
        var usersToAdd = new List<UserDto>
        {
            new() { Id = USER_1 },
            new() { Id = USER_2 },
            new() { Id = 45, Username = "User3" }
        };
        var result = await this._dbContext.Users.AddMissingUsersAsync(usersToAdd, default);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        var users = await this._dbContext.Users
            .Include(x => x.UsernameHistories)
            .FirstOrDefaultAsync(x => x.Id == 45);
        users.Should().NotBeNull();
        users!.UsernameHistories.Should().HaveCount(1)
            .And.AllSatisfy(x => x.Username.Should().Be("User3"));
    }

    [Fact]
    public async Task WhenUserNamesAreNotInDatabase_AddThemAsync()
    {
        var usersToAdd = new List<UserDto>
        {
            new() { Id = USER_1, Username = "Username2" }
        };
        var result = await this._dbContext.UsernameHistory.AddMissingUsernameHistoryAsync(usersToAdd, default);

        await this._dbContext.SaveChangesAsync();

        result.Should().BeTrue();
        var users = await this._dbContext.UsernameHistory
            .Where(x => x.UserId == USER_1)
            .ToListAsync();
        users.Should().NotBeNull();
        users.Should().HaveCount(2)
            .And.AllSatisfy(x => x.Username.Should().BeOneOf("Username2", "User1"));
    }
}

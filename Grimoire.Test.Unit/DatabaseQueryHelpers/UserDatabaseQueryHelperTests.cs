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
using EntityFramework.Exceptions.PostgreSQL;
using FluentAssertions;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Domain;
using Grimoire.Features.Shared.SharedDtos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class UserDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const long User1 = 1;
    private const long User2 = 2;

    private readonly GrimoireDbContext _dbContext = new GrimoireDbContext(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .UseExceptionProcessor()
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new User { Id = User1 });
        await this._dbContext.AddAsync(new UsernameHistory
        {
            UserId = User1, Username = "User1", Timestamp = DateTime.UtcNow.AddMinutes(-2)
        });
        await this._dbContext.AddAsync(new User { Id = User2 });
        await this._dbContext.AddAsync(new UsernameHistory
        {
            UserId = User2, Username = "User2", Timestamp = DateTime.UtcNow.AddMinutes(-2)
        });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenUsersAreNotInDatabase_AddThemAsync()
    {
        var usersToAdd = new List<UserDto>
        {
            new() { Id = User1 }, new() { Id = User2 }, new() { Id = 45, Username = "User3" }
        };
        var result = await this._dbContext.Users.AddMissingUsersAsync(usersToAdd);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        var users = await this._dbContext.Users
            .Include(x => x.UsernameHistories)
            .FirstOrDefaultAsync(x => x.Id == 45);
        users.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenNoUsersAreAdded_ReturnsFalse()
    {
        // Arrange
        var usersToAdd = Array.Empty<UserDto>(); // No members to add

        // Act
        var result = await this._dbContext.Users.AddMissingUsersAsync(usersToAdd);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenUserNamesAreNotInDatabase_AddThemAsync()
    {
        var usersToAdd = new List<UserDto> { new() { Id = User1, Username = "Username2" } };
        var result = await this._dbContext.UsernameHistory.AddMissingUsernameHistoryAsync(usersToAdd);

        await this._dbContext.SaveChangesAsync();

        result.Should().BeTrue();
        var users = await this._dbContext.UsernameHistory
            .Where(x => x.UserId == User1)
            .ToListAsync();
        users.Should().NotBeNull()
            .And.HaveCount(2)
            .And.AllSatisfy(x => x.Username.Should().BeOneOf("Username2", "User1"));
    }

    [Fact]
    public async Task WhenNoUsernamesAreAdded_ReturnsFalse()
    {
        // Arrange
        var usersToAdd = Array.Empty<UserDto>(); // No members to add

        // Act
        var result = await this._dbContext.UsernameHistory.AddMissingUsernameHistoryAsync(usersToAdd);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddMissingUsernameHistoryAsync_SavesNewUsernameOnlyWhenDifferent()
    {
        // Arrange
        var initialUsername = new UsernameHistory { UserId = User1, Username = "ChangedUsername" };
        await this._dbContext.UsernameHistory.AddAsync(initialUsername);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();

        var usersToAdd = new List<UserDto> { new() { Id = User1, Username = "User1" } };

        // Act
        var result = await this._dbContext.UsernameHistory.AddMissingUsernameHistoryAsync(usersToAdd);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();

        // Assert
        result.Should().BeTrue();

        var usernames = await this._dbContext.UsernameHistory
            .Where(x => x.UserId == User1)
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync();

        usernames.Should().HaveCount(3);
        usernames.First().Username.Should().Be("User1");
        usernames.Last().Username.Should().Be("User1");
    }
}

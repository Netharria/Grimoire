// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Exceptions;
using Grimoire.Features.Logging.UserLogging.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.UserLogging.Queries;

[Collection("Test collection")]
public sealed class GetRecentUserAndNickNamesTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong UserId = 1;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GuildId, UserLogSettings = new GuildUserLogSettings { ModuleEnabled = true }
        });
        await this._dbContext.AddAsync(new User { Id = UserId });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task GivenAUserHasNotBeenOnTheServer_WhenGetNamesIsCalled_ThrowAnticipatedException()
    {
        //Arrange
        var cut = new GetRecentUserAndNickNames.Handler(this._dbContext);
        var query = new GetRecentUserAndNickNames.Query { UserId = UserId, GuildId = GuildId };

        //Act
        var result = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(query, default));

        //Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Could not find that user. Have they been on the server before?");
    }

    [Fact]
    public async Task GivenAGuildDoesNotHaveTheModuleEnabled_WhenGetNamesIsCalled_ReturnNull()
    {
        //Arrange
        await this._dbContext.Guilds.AddAsync(new Guild
        {
            Id = 1234, UserLogSettings = new GuildUserLogSettings { ModuleEnabled = false }
        });
        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = 1234 });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetRecentUserAndNickNames.Handler(this._dbContext);
        var query = new GetRecentUserAndNickNames.Query { UserId = UserId, GuildId = 1234 };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenAUserDoesNotHaveUserOrNickNames_WhenGetNamesIsCalled_ReturnEmptyLists()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetRecentUserAndNickNames.Handler(this._dbContext);
        var query = new GetRecentUserAndNickNames.Query { UserId = UserId, GuildId = GuildId };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Usernames.Should().NotBeNull()
            .And.BeEmpty();
        result!.Nicknames.Should().NotBeNull()
            .And.BeEmpty();
    }

    [Fact]
    public async Task GivenAUserHasOneUserAndNickName_WhenGetNamesIsCalled_ReturnListOfOneItem()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.UsernameHistory.AddAsync(new UsernameHistory
        {
            UserId = UserId, Username = "User1", Timestamp = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await this._dbContext.NicknameHistory.AddAsync(new NicknameHistory
        {
            UserId = UserId, GuildId = GuildId, Nickname = "Nick1", Timestamp = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetRecentUserAndNickNames.Handler(this._dbContext);
        var query = new GetRecentUserAndNickNames.Query { UserId = UserId, GuildId = GuildId };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Usernames.Should().NotBeNull()
            .And.HaveCount(1)
            .And.Contain("User1");
        result!.Nicknames.Should().NotBeNull()
            .And.HaveCount(1)
            .And.Contain("Nick1");
    }

    [Fact]
    public async Task GivenAUserHasSeveralUserAndNickName_WhenGetNamesIsCalled_ReturnListOfThreeIMostRecentItems()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.UsernameHistory.AddRangeAsync(
            new UsernameHistory { UserId = UserId, Username = "User1", Timestamp = DateTimeOffset.UtcNow.AddDays(-4) },
            new UsernameHistory { UserId = UserId, Username = "User2", Timestamp = DateTimeOffset.UtcNow.AddDays(-3) },
            new UsernameHistory { UserId = UserId, Username = "User3", Timestamp = DateTimeOffset.UtcNow.AddDays(-2) },
            new UsernameHistory { UserId = UserId, Username = "User4", Timestamp = DateTimeOffset.UtcNow.AddDays(-1) });
        await this._dbContext.NicknameHistory.AddRangeAsync(
            new NicknameHistory
            {
                UserId = UserId,
                GuildId = GuildId,
                Nickname = "Nick1",
                Timestamp = DateTimeOffset.UtcNow.AddDays(-4)
            },
            new NicknameHistory
            {
                UserId = UserId,
                GuildId = GuildId,
                Nickname = "Nick2",
                Timestamp = DateTimeOffset.UtcNow.AddDays(-3)
            },
            new NicknameHistory
            {
                UserId = UserId,
                GuildId = GuildId,
                Nickname = "Nick3",
                Timestamp = DateTimeOffset.UtcNow.AddDays(-2)
            },
            new NicknameHistory
            {
                UserId = UserId,
                GuildId = GuildId,
                Nickname = "Nick4",
                Timestamp = DateTimeOffset.UtcNow.AddDays(-1)
            });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetRecentUserAndNickNames.Handler(this._dbContext);
        var query = new GetRecentUserAndNickNames.Query { UserId = UserId, GuildId = GuildId };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Usernames.Should().NotBeNull()
            .And.HaveCount(3)
            .And.ContainInConsecutiveOrder("User4", "User3", "User2");
        result!.Nicknames.Should().NotBeNull()
            .And.HaveCount(3)
            .And.ContainInOrder("Nick4", "Nick3", "Nick2");
    }
}

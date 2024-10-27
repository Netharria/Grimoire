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
using Grimoire.Features.Leveling.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Queries;

[Collection("Test collection")]
public sealed class GetUserLevelingInfoTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong UserId = 1;
    private const ulong Role1 = 1;
    private const ulong Role2 = 2;
    private const ulong Role3 = 3;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GuildId, LevelSettings = new GuildLevelSettings { ModuleEnabled = true }
        });
        await this._dbContext.AddAsync(new User { Id = UserId });
        await this._dbContext.AddRangeAsync(
            new Role { Id = Role1, GuildId = GuildId },
            new Role { Id = Role2, GuildId = GuildId },
            new Role { Id = Role3, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task GivenAUserHasNotzBeenOnTheServer_WhenGetUserLevelingInfoIsCalled_ThrowAnticipatedException()
    {
        //Arrange
        var cut = new GetUserLevelingInfo.Handler(this._dbContext);
        var query = new GetUserLevelingInfo.Query { UserId = UserId, GuildId = GuildId, RoleIds = [] };

        //Act
        var result = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(query, default));

        //Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Could not find that user. Have they been on the server before?");
    }

    [Fact]
    public async Task GivenAGuildDoesNotHaveTheModuleEnabled_WhenGetUserLevelingInfoIsCalled_ReturnNull()
    {
        //Arrange
        await this._dbContext.Guilds.AddAsync(new Guild
        {
            Id = 1234, LevelSettings = new GuildLevelSettings { ModuleEnabled = false }
        });
        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = 1234 });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserLevelingInfo.Handler(this._dbContext);
        var query = new GetUserLevelingInfo.Query { UserId = UserId, GuildId = 1234, RoleIds = [] };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenAUserDoesNotHaveXp_WhenGetUserLevelingInfoIsCalled_ReturnBaseValues()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserLevelingInfo.Handler(this._dbContext);
        var query = new GetUserLevelingInfo.Query { UserId = UserId, GuildId = GuildId, RoleIds = [] };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Level.Should().Be(1);
        result!.IsXpIgnored.Should().BeFalse();
        result!.EarnedRewards.Should().NotBeNull()
            .And.BeEmpty();
    }

    [Fact]
    public async Task GivenAUserHasXp_WhenGetUserLevelingInfoIsCalled_ReturnLevelValues()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.XpHistory.AddAsync(new XpHistory
        {
            UserId = UserId,
            GuildId = GuildId,
            TimeOut = DateTime.UtcNow.AddMinutes(-1),
            Type = XpHistoryType.Earned,
            Xp = 300
        });
        await this._dbContext.Rewards.AddRangeAsync(
            new Reward { GuildId = GuildId, RoleId = Role1, RewardLevel = 5 },
            new Reward { GuildId = GuildId, RoleId = Role2, RewardLevel = 10 },
            new Reward { GuildId = GuildId, RoleId = Role3, RewardLevel = 8 });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserLevelingInfo.Handler(this._dbContext);
        var query = new GetUserLevelingInfo.Query { UserId = UserId, GuildId = GuildId, RoleIds = [] };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Level.Should().Be(8);
        result!.IsXpIgnored.Should().BeFalse();
        result!.EarnedRewards.Should().NotBeNull()
            .And.HaveCount(2)
            .And.ContainInOrder(Role1, Role3);
    }

    [Fact]
    public async Task GivenAUserIsIgnored_WhenGetUserLevelingInfoIsCalled_ReturnIsIgnoredValue()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.IgnoredMembers.AddAsync(new IgnoredMember { UserId = UserId, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserLevelingInfo.Handler(this._dbContext);
        var query = new GetUserLevelingInfo.Query { UserId = UserId, GuildId = GuildId, RoleIds = [] };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Level.Should().Be(1);
        result!.IsXpIgnored.Should().BeTrue();
        result!.EarnedRewards.Should().NotBeNull()
            .And.BeEmpty();
    }

    [Fact]
    public async Task GivenAUserHasAnIgnoredRole_WhenGetUserLevelingInfoIsCalled_ReturnIsIgnoredValue()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.IgnoredRoles.AddAsync(new IgnoredRole { RoleId = Role1, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserLevelingInfo.Handler(this._dbContext);
        var query = new GetUserLevelingInfo.Query { UserId = UserId, GuildId = GuildId, RoleIds = [Role1] };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Level.Should().Be(1);
        result!.IsXpIgnored.Should().BeTrue();
        result!.EarnedRewards.Should().NotBeNull()
            .And.BeEmpty();
    }

    [Fact]
    public async Task GivenAUserHasRolesThatAreNotIgnored_WhenGetUserLevelingInfoIsCalled_ReturnIsNotIgnoredValue()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserLevelingInfo.Handler(this._dbContext);
        var query = new GetUserLevelingInfo.Query { UserId = UserId, GuildId = GuildId, RoleIds = [Role1, Role2] };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Level.Should().Be(1);
        result!.IsXpIgnored.Should().BeFalse();
        result!.EarnedRewards.Should().NotBeNull()
            .And.BeEmpty();
    }

    [Fact]
    public async Task
        GivenAUserHasOneRoleThatIsIgnoredAndOneThatIsNot_WhenGetUserLevelingInfoIsCalled_ReturnIsIgnoredValue()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.IgnoredRoles.AddAsync(new IgnoredRole { RoleId = Role1, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserLevelingInfo.Handler(this._dbContext);
        var query = new GetUserLevelingInfo.Query { UserId = UserId, GuildId = GuildId, RoleIds = [Role1, Role2] };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Level.Should().Be(1);
        result!.IsXpIgnored.Should().BeTrue();
        result!.EarnedRewards.Should().NotBeNull()
            .And.BeEmpty();
    }
}

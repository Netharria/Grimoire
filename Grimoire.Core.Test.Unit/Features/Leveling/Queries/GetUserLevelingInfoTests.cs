// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Queries;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Queries;

[Collection("Test collection")]
public class GetUserLevelingInfoTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
    .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong USER_ID = 1;
    private const ulong ROLE_1 = 1;
    private const ulong ROLE_2 = 2;
    private const ulong ROLE_3 = 3;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GUILD_ID,
            LevelSettings = new GuildLevelSettings
            {
                ModuleEnabled = true
            }
        });
        await this._dbContext.AddAsync(new User { Id = USER_ID });
        await this._dbContext.AddRangeAsync(
            new Role
            {
                Id = ROLE_1,
                GuildId = GUILD_ID
            },
            new Role
            {
                Id = ROLE_2,
                GuildId = GUILD_ID
            },
            new Role
            {
                Id = ROLE_3,
                GuildId = GUILD_ID
            });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task GivenAUserHasNotzBeenOnTheServer_WhenGetUserLevelingInfoIsCalled_ThrowAnticipatedException()
    {
        //Arrange
        var CUT = new GetUserLevelingInfo.Handler(_dbContext);
        var query = new GetUserLevelingInfo.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            RoleIds = []
        };

        //Act
        var result = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(query, default));

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
            Id = 1234,
            LevelSettings = new GuildLevelSettings
            {
                ModuleEnabled = false
            }
        });
        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = 1234 });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserLevelingInfo.Handler(_dbContext);
        var query = new GetUserLevelingInfo.Query
        {
            UserId = USER_ID,
            GuildId = 1234,
            RoleIds = []
        };

        //Act

        var result = await CUT.Handle(query, default);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenAUserDoesNotHaveXp_WhenGetUserLevelingInfoIsCalled_ReturnBaseValues()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserLevelingInfo.Handler(_dbContext);
        var query = new GetUserLevelingInfo.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            RoleIds = []
        };

        //Act

        var result = await CUT.Handle(query, default);

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

        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.XpHistory.AddAsync(new XpHistory
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            TimeOut = DateTime.UtcNow.AddMinutes(-1),
            Type = XpHistoryType.Earned,
            Xp = 300
        });
        await this._dbContext.Rewards.AddRangeAsync(
            new Reward
            {
                GuildId = GUILD_ID,
                RoleId = ROLE_1,
                RewardLevel = 5,
            },
            new Reward
            {
                GuildId = GUILD_ID,
                RoleId = ROLE_2,
                RewardLevel = 10,
            },
            new Reward
            {
                GuildId = GUILD_ID,
                RoleId = ROLE_3,
                RewardLevel = 8
            });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserLevelingInfo.Handler(_dbContext);
        var query = new GetUserLevelingInfo.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            RoleIds = []
        };

        //Act

        var result = await CUT.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Level.Should().Be(8);
        result!.IsXpIgnored.Should().BeFalse();
        result!.EarnedRewards.Should().NotBeNull()
            .And.HaveCount(2)
            .And.ContainInOrder(ROLE_1, ROLE_3);
    }

    [Fact]
    public async Task GivenAUserIsIgnored_WhenGetUserLevelingInfoIsCalled_ReturnIsIgnoredValue()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.IgnoredMembers.AddAsync(new IgnoredMember { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserLevelingInfo.Handler(_dbContext);
        var query = new GetUserLevelingInfo.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            RoleIds = []
        };

        //Act

        var result = await CUT.Handle(query, default);

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

        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.IgnoredRoles.AddAsync(new IgnoredRole { RoleId = ROLE_1, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserLevelingInfo.Handler(_dbContext);
        var query = new GetUserLevelingInfo.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            RoleIds = [ ROLE_1 ]
        };

        //Act

        var result = await CUT.Handle(query, default);

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

        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserLevelingInfo.Handler(_dbContext);
        var query = new GetUserLevelingInfo.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            RoleIds = [ ROLE_1, ROLE_2 ]
        };

        //Act

        var result = await CUT.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Level.Should().Be(1);
        result!.IsXpIgnored.Should().BeFalse();
        result!.EarnedRewards.Should().NotBeNull()
            .And.BeEmpty();
    }

    [Fact]
    public async Task GivenAUserHasOneRoleThatIsIgnoredAndOneThatIsNot_WhenGetUserLevelingInfoIsCalled_ReturnIsIgnoredValue()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.IgnoredRoles.AddAsync(new IgnoredRole { RoleId = ROLE_1, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserLevelingInfo.Handler(_dbContext);
        var query = new GetUserLevelingInfo.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            RoleIds = [ ROLE_1, ROLE_2 ]
        };

        //Act

        var result = await CUT.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.Level.Should().Be(1);
        result!.IsXpIgnored.Should().BeTrue();
        result!.EarnedRewards.Should().NotBeNull()
            .And.BeEmpty();
    }
}

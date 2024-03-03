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
public sealed class GetLevelQueryTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong USER_ID = 1;
    private const ulong ROLE_ID_1 = 1;
    private const ulong ROLE_ID_2 = 2;
    private const ulong ROLE_ID_3 = 3;
    private const int REWARD_LEVEL_1 = 100;
    private const int REWARD_LEVEL_2 = 10;
    private const int REWARD_LEVEL_3 = 110;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GUILD_ID,
            LevelSettings = new GuildLevelSettings()
        });
        await this._dbContext.AddAsync(new User { Id = USER_ID });
        await this._dbContext.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            Xp = 300,
            Type = XpHistoryType.Awarded,
            TimeOut = DateTime.UtcNow.AddMinutes(-5),
        });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            Xp = 300,
            Type = XpHistoryType.Awarded,
            TimeOut = DateTime.UtcNow.AddMinutes(-5),
        });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID_1, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Reward
        {
            RoleId = ROLE_ID_1,
            GuildId = GUILD_ID,
            RewardLevel = REWARD_LEVEL_1
        });

        await this._dbContext.AddAsync(new Role { Id = ROLE_ID_2, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Reward
        {
            RoleId = ROLE_ID_2,
            GuildId = GUILD_ID,
            RewardLevel = REWARD_LEVEL_2
        });

        await this._dbContext.AddAsync(new Role { Id = ROLE_ID_3, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Reward
        {
            RoleId = ROLE_ID_3,
            GuildId = GUILD_ID,
            RewardLevel = REWARD_LEVEL_3
        });

        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenCallingGetLevelQueryHandler_IfUserDoesNotExist_ReturnFailedResponse()
    {

        var CUT = new GetLevel.Handler(this._dbContext);
        var command = new GetLevel.Query
        {
            GuildId = GUILD_ID,
            UserId = 234081234
        };


        var response = await CUT.Invoking(async x => await x.Handle(command, default))
            .Should().ThrowAsync<AnticipatedException>()
            .WithMessage("That user could not be found.");
    }

    [Fact]
    public async Task WhenCallingGetLevelQueryHandler_IfUserExists_ReturnResponseAsync()
    {

        var CUT = new GetLevel.Handler(this._dbContext);
        var command = new GetLevel.Query
        {
            GuildId = GUILD_ID,
            UserId = USER_ID
        };

        var response = await CUT.Handle(command, default);

        response.UsersXp.Should().Be(600);
        response.UsersLevel.Should().Be(10);
        response.LevelProgress.Should().Be(105);
        response.XpForNextLevel.Should().Be(132);
        response.NextRoleRewardId.Should().Be(ROLE_ID_1);
        response.NextRewardLevel.Should().Be(REWARD_LEVEL_1);
    }

    [Fact]
    public async Task WhenCallingGetLevelQueryHandler_IfUserHasAHigherLevelThanTheMaxReward_ReturnResponseWithNoNextRewardAsync()
    {
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            Xp = 312231654654659L,
            Type = XpHistoryType.Awarded,
            TimeOut = DateTime.UtcNow.AddMinutes(-5),
        });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetLevel.Handler(this._dbContext);
        var command = new GetLevel.Query
        {
            GuildId = GUILD_ID,
            UserId = USER_ID
        };

        var response = await CUT.Handle(command, default);

        response.UsersXp.Should().Be(312231654655259);
        response.UsersLevel.Should().Be(6452202);
        response.LevelProgress.Should().Be(18355244);
        response.XpForNextLevel.Should().Be(100009108);
        response.NextRoleRewardId.Should().BeNull();
        response.NextRewardLevel.Should().BeNull();
    }
}

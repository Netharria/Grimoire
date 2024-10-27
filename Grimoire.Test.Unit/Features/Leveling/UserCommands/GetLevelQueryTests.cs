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
using Grimoire.Features.Leveling.UserCommands;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.UserCommands;

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
    private const ulong ROLE_ID = 1;
    private const int REWARD_LEVEL = 100;

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
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Reward
        {
            RoleId = ROLE_ID,
            GuildId = GUILD_ID,
            RewardLevel = REWARD_LEVEL
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

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("That user could not be found.");
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

        response.UsersXp.Should().Be(300);
        response.UsersLevel.Should().Be(8);
        response.LevelProgress.Should().Be(15);
        response.XpForNextLevel.Should().Be(94);
        response.NextRoleRewardId.Should().Be(ROLE_ID);
        response.NextRewardLevel.Should().Be(REWARD_LEVEL);
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grimoire.Features.Leveling.Events;
using Xunit;
using Grimoire.Domain;
using FluentAssertions;

namespace Grimoire.Test.Unit.Features.Leveling.Events;

[Collection("Test collection")]
public class CheckIfUserEarnedRewardsTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong GUILD_ID_2 = 2;
    private const ulong CHANNEL_ID = 1;
    private const ulong ROLE_ID_1 = 1;
    private const int REWARD_LEVEL_1 = 1;
    private const ulong ROLE_ID_2 = 2;
    private const int REWARD_LEVEL_2 = 3;
    private const ulong ROLE_ID_3 = 3;
    private const ulong ROLE_ID_4 = 4;
    private const int REWARD_LEVEL_3 = 5;
    private const int GAIN_AMOUNT = 15;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GUILD_ID,
            LevelSettings = new GuildLevelSettings
            {
                LevelChannelLogId = CHANNEL_ID,
                ModuleEnabled = true,
                Amount = GAIN_AMOUNT
            }
        });
        await this._dbContext.AddAsync(new Guild
        {
            Id = GUILD_ID_2,
            LevelSettings = new GuildLevelSettings
            {
                ModuleEnabled = true,
                Amount = GAIN_AMOUNT
            }
        });
        await this._dbContext.AddAsync(new Channel { Id = CHANNEL_ID, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID_1, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID_2, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID_3, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID_4, GuildId = GUILD_ID_2 });
        await this._dbContext.AddAsync(new Reward { RoleId = ROLE_ID_1, GuildId = GUILD_ID, RewardLevel = REWARD_LEVEL_1, RewardMessage = "Test1" });
        await this._dbContext.AddAsync(new Reward { RoleId = ROLE_ID_2, GuildId = GUILD_ID, RewardLevel = REWARD_LEVEL_2, RewardMessage = "Test2" });
        await this._dbContext.AddAsync(new Reward { RoleId = ROLE_ID_3, GuildId = GUILD_ID, RewardLevel = REWARD_LEVEL_3, RewardMessage = "Test3" });

        await this._dbContext.AddAsync(new Reward { RoleId = ROLE_ID_4, GuildId = GUILD_ID_2, RewardLevel = REWARD_LEVEL_1, RewardMessage = "Test3" });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task Handle_ReturnsRewardsFromCorrectGuild()
    {
        // Arrange
        var request = new CheckIfUserEarnedReward.Request
        {
            GuildId = GUILD_ID,
            UserLevel = 3
        };
        var handler = new CheckIfUserEarnedReward.RequestHandler(_dbContext);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert

        // Assert
        result!.Should().NotBeNull();
        result!.EarnedRewards.Should().NotBeNullOrEmpty()
            .And.HaveCount(2)
            .And.Contain(r => r.RoleId == ROLE_ID_1 && r.Message == "Test1")
            .And.Contain(r => r.RoleId == ROLE_ID_2 && r.Message == "Test2");

    }
}

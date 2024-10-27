// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Features.Leveling.Events;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Events;

[Collection("Test collection")]
public class CheckIfUserEarnedRewardsTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong GuildId2 = 2;
    private const ulong ChannelId = 1;
    private const ulong RoleId1 = 1;
    private const int RewardLevel1 = 1;
    private const ulong RoleId2 = 2;
    private const int RewardLevel2 = 3;
    private const ulong RoleId3 = 3;
    private const ulong RoleId4 = 4;
    private const int RewardLevel3 = 5;
    private const int GainAmount = 15;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GuildId,
            LevelSettings = new GuildLevelSettings
            {
                LevelChannelLogId = ChannelId, ModuleEnabled = true, Amount = GainAmount
            }
        });
        await this._dbContext.AddAsync(new Guild
        {
            Id = GuildId2, LevelSettings = new GuildLevelSettings { ModuleEnabled = true, Amount = GainAmount }
        });
        await this._dbContext.AddAsync(new Channel { Id = ChannelId, GuildId = GuildId });
        await this._dbContext.AddAsync(new Role { Id = RoleId1, GuildId = GuildId });
        await this._dbContext.AddAsync(new Role { Id = RoleId2, GuildId = GuildId });
        await this._dbContext.AddAsync(new Role { Id = RoleId3, GuildId = GuildId });
        await this._dbContext.AddAsync(new Role { Id = RoleId4, GuildId = GuildId2 });
        await this._dbContext.AddAsync(new Reward
        {
            RoleId = RoleId1, GuildId = GuildId, RewardLevel = RewardLevel1, RewardMessage = "Test1"
        });
        await this._dbContext.AddAsync(new Reward
        {
            RoleId = RoleId2, GuildId = GuildId, RewardLevel = RewardLevel2, RewardMessage = "Test2"
        });
        await this._dbContext.AddAsync(new Reward
        {
            RoleId = RoleId3, GuildId = GuildId, RewardLevel = RewardLevel3, RewardMessage = "Test3"
        });

        await this._dbContext.AddAsync(new Reward
        {
            RoleId = RoleId4, GuildId = GuildId2, RewardLevel = RewardLevel1, RewardMessage = "Test3"
        });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task Handle_ReturnsRewardsFromCorrectGuild()
    {
        // Arrange
        var request = new CheckIfUserEarnedReward.Request { GuildId = GuildId, UserLevel = 3 };
        var handler = new CheckIfUserEarnedReward.RequestHandler(this._dbContext);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert

        // Assert
        result!.Should().NotBeNull();
        result!.EarnedRewards.Should().NotBeNullOrEmpty()
            .And.HaveCount(2)
            .And.Contain(r => r.RoleId == RoleId1 && r.Message == "Test1")
            .And.Contain(r => r.RoleId == RoleId2 && r.Message == "Test2");
    }
}

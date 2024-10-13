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
using Grimoire.Features.Leveling.Rewards;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Commands;

[Collection("Test collection")]
public sealed class AddRewardCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong ROLE_ID = 1;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();


    [Fact]
    public async Task WhenAddingReward_IfRewardDoesntExist_AddRoleAsync()
    {

        var CUT = new AddReward.Handler(this._dbContext);
        var command = new AddReward.Request
        {
            RoleId = ROLE_ID,
            GuildId = GUILD_ID,
            RewardLevel = 10,
            Message = "Test"
        };

        var response = await CUT.Handle(command, default);

        this._dbContext.ChangeTracker.Clear();

        var reward = await this._dbContext.Rewards.FirstOrDefaultAsync(x => x.RoleId == ROLE_ID);

        response.Message.Should().Be($"Added <@&{ROLE_ID}> reward at level 10");
        reward.Should().NotBeNull();
        reward!.GuildId.Should().Be(GUILD_ID);
        reward.RewardLevel.Should().Be(10);
        reward.RewardMessage.Should().Be("Test");
    }

    [Fact]
    public async Task WhenAddingReward_IfRewardExist_UpdateRoleAsync()
    {
        await this._dbContext.AddAsync(new Reward
        {
            RoleId = ROLE_ID,
            GuildId = GUILD_ID,
            RewardLevel = 10
        });
        await this._dbContext.SaveChangesAsync();

        var CUT = new AddReward.Handler(this._dbContext);
        var command = new AddReward.Request
        {
            RoleId = ROLE_ID,
            GuildId = GUILD_ID,
            RewardLevel = 15,
            Message = "Test"
        };

        var response = await CUT.Handle(command, default);

        this._dbContext.ChangeTracker.Clear();

        var reward = await this._dbContext.Rewards.FirstOrDefaultAsync(x => x.RoleId == ROLE_ID);

        response.Message.Should().Be($"Updated <@&{ROLE_ID}> reward to level 15");

        reward.Should().NotBeNull();
        reward!.GuildId.Should().Be(GUILD_ID);
        reward.RewardLevel.Should().Be(15);
        reward.RewardMessage.Should().Be("Test");
    }
}

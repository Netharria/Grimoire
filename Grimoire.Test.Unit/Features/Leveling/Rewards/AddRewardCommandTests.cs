// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using EntityFramework.Exceptions.PostgreSQL;
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Features.Leveling.Rewards;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Rewards;

[Collection("Test collection")]
public sealed class AddRewardCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong RoleId = 1;

    private readonly Func<GrimoireDbContext> _createDbContext = () => new GrimoireDbContext(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .UseExceptionProcessor()
            .Options);

    private readonly IDbContextFactory<GrimoireDbContext> _mockDbContextFactory =
        Substitute.For<IDbContextFactory<GrimoireDbContext>>();

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Guild { Id = GuildId });
        await dbContext.AddAsync(new Role { Id = RoleId, GuildId = GuildId });
        await dbContext.SaveChangesAsync();

        this._mockDbContextFactory.CreateDbContextAsync().Returns(this._createDbContext());
    }

    public Task DisposeAsync() => this._resetDatabase();


    [Fact]
    public async Task WhenAddingReward_IfRewardDoesntExist_AddRoleAsync()
    {
        await using var dbContext = this._createDbContext();
        var cut = new AddReward.Handler(this._mockDbContextFactory);
        var command = new AddReward.Request { RoleId = RoleId, GuildId = GuildId, RewardLevel = 10, Message = "Test" };

        var response = await cut.Handle(command, default);

        dbContext.ChangeTracker.Clear();

        var reward = await dbContext.Rewards.FirstOrDefaultAsync(x => x.RoleId == RoleId);

        response.Message.Should().Be($"Added <@&{RoleId}> reward at level 10");
        reward.Should().NotBeNull();
        reward!.GuildId.Should().Be(GuildId);
        reward.RewardLevel.Should().Be(10);
        reward.RewardMessage.Should().Be("Test");
    }

    [Fact]
    public async Task WhenAddingReward_IfRewardExist_UpdateRoleAsync()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Reward { RoleId = RoleId, GuildId = GuildId, RewardLevel = 10 });
        await dbContext.SaveChangesAsync();

        var cut = new AddReward.Handler(this._mockDbContextFactory);
        var command = new AddReward.Request { RoleId = RoleId, GuildId = GuildId, RewardLevel = 15, Message = "Test" };

        var response = await cut.Handle(command, default);

        dbContext.ChangeTracker.Clear();

        var reward = await dbContext.Rewards.FirstOrDefaultAsync(x => x.RoleId == RoleId);

        response.Message.Should().Be($"Updated <@&{RoleId}> reward to level 15");

        reward.Should().NotBeNull();
        reward!.GuildId.Should().Be(GuildId);
        reward.RewardLevel.Should().Be(15);
        reward.RewardMessage.Should().Be("Test");
    }
}

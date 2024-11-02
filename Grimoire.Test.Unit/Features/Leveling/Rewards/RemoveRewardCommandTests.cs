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
using Grimoire.Exceptions;
using Grimoire.Features.Leveling.Rewards;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Rewards;

[Collection("Test collection")]
public sealed class RemoveRewardCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
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
    public async Task WhenRemovingReward_IfRewardExists_RemoveRoleAsync()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Reward { RoleId = RoleId, GuildId = GuildId, RewardLevel = 10 });
        await dbContext.SaveChangesAsync();

        var cut = new RemoveReward.Handler(this._mockDbContextFactory);
        var command = new RemoveReward.Request { RoleId = RoleId };

        var response = await cut.Handle(command, default);

        response.Message.Should().Be($"Removed <@&{RoleId}> reward");

        dbContext.ChangeTracker.Clear();

        var reward = await dbContext.Rewards.FirstOrDefaultAsync(x => x.RoleId == RoleId);

        reward.Should().BeNull();
    }

    [Fact]
    public async Task WhenAddingReward_IfRewardExist_UpdateRole()
    {
        var cut = new RemoveReward.Handler(this._mockDbContextFactory);
        var command = new RemoveReward.Request { RoleId = RoleId };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(command, default));

        response.Should().NotBeNull();
        response.Message.Should().Be($"Did not find a saved reward for role <@&{RoleId}>");
    }
}

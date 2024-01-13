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
using Grimoire.Core.Features.Leveling.Commands;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Commands;

[Collection("Test collection")]
public sealed class RemoveRewardCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
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
    public async Task WhenRemovingReward_IfRewardExists_RemoveRoleAsync()
    {
        await this._dbContext.AddAsync(new Reward
        {
            RoleId = ROLE_ID,
            GuildId = GUILD_ID,
            RewardLevel = 10
        });
        await this._dbContext.SaveChangesAsync();

        var CUT = new RemoveReward.Handler(this._dbContext);
        var command = new RemoveReward.Command
        {
            RoleId = ROLE_ID
        };

        var response = await CUT.Handle(command, default);

        response.Message.Should().Be($"Removed <@&{ROLE_ID}> reward");

        this._dbContext.ChangeTracker.Clear();

        var reward = await this._dbContext.Rewards.FirstOrDefaultAsync(x => x.RoleId == ROLE_ID);

        reward.Should().BeNull();
    }

    [Fact]
    public async Task WhenAddingReward_IfRewardExist_UpdateRole()
    {
        var CUT = new RemoveReward.Handler(this._dbContext);
        var command = new RemoveReward.Command
        {
            RoleId = ROLE_ID
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async() => await CUT.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be($"Did not find a saved reward for role <@&{ROLE_ID}>");
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Features.Leveling.Commands.ManageRewardsCommands.AddReward;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Commands.ManageRewardsCommands.AddReward;

[TestFixture]
public class AddRewardCommandHandlerTests
{
    [Test]
    public async Task WhenAddingReward_IfRewardDoesntExist_AddRoleAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();

        var CUT = new AddRewardCommandHandler(context);
        var command = new AddRewardCommand
        {
            RoleId = TestDatabaseFixture.Role1.Id,
            GuildId = TestDatabaseFixture.Role1.GuildId,
            RewardLevel = 10
        };

        var response = await CUT.Handle(command, default);

        context.ChangeTracker.Clear();

        response.Message.Should().Be("Added <@&6> reward at level 10");
    }

    [Test]
    public async Task WhenAddingReward_IfRewardExist_UpdateRoleAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();

        context.Database.BeginTransaction();

        var CUT = new AddRewardCommandHandler(context);
        var command = new AddRewardCommand
        {
            RoleId = TestDatabaseFixture.Role2.Id,
            GuildId = TestDatabaseFixture.Role2.GuildId,
            RewardLevel = 15
        };

        var response = await CUT.Handle(command, default);

        context.ChangeTracker.Clear();
        response.Message.Should().Be("Updated <@&7> reward to level 15");
    }
}

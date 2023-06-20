// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Commands.ManageRewardsCommands.RemoveReward;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Commands.ManageRewardsCommands.RemoveReward;

[TestFixture]
public class AddRewardCommandHandlerTests
{

    [Test]
    public async Task WhenRemovingReward_IfRewardExists_RemoveRoleAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var CUT = new RemoveRewardCommandHandler(context);
        var command = new RemoveRewardCommand
        {
            RoleId = TestDatabaseFixture.Role2.Id
        };

        var response = await CUT.Handle(command, default);

        context.ChangeTracker.Clear();
        response.Message.Should().Be("Removed <@&7> reward");
    }

    [Test]
    public void WhenAddingReward_IfRewardExist_UpdateRole()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var CUT = new RemoveRewardCommandHandler(context);
        var command = new RemoveRewardCommand
        {
            RoleId = TestDatabaseFixture.Role1.Id
        };

        var response = Assert.ThrowsAsync<AnticipatedException>(async() => await CUT.Handle(command, default));

        context.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response?.Message.Should().Be("Did not find a saved reward for role <@&6>");
    }
}

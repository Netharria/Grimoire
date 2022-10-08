// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Cybermancy.Core.Features.Leveling.Commands.ManageRewardsCommands.AddReward;
using FluentAssertions;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.Features.Leveling.Commands.ManageRewardsCommands.AddReward
{
    [TestFixture]
    public class AddRewardCommandHandlerTests
    {

        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();
        [Test]
        public async Task WhenAddingReward_IfRewardDoesntExist_AddRoleAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
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

            response.Success.Should().BeTrue();
            response.Message.Should().Be("Added <@&6> reward at level 10");
        }

        [Test]
        public async Task WhenAddingReward_IfRewardExist_UpdateRoleAsync()
        {
            var context = this.DatabaseFixture.CreateContext();

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

            response.Success.Should().BeTrue();
            response.Message.Should().Be("Updated <@&7> reward to level 15");
        }
    }
}

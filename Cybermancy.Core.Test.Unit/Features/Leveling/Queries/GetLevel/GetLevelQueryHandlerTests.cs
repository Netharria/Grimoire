// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Cybermancy.Core.Features.Leveling.Queries.GetLevel;
using FluentAssertions;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.Features.Leveling.Queries.GetLevel
{
    [TestFixture]
    public class GetLevelQueryHandlerTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhenCallingGetLevelQueryHandler_IfUserDoesNotExist_ReturnFailedResponseAsync()
        {
            var context = this.DatabaseFixture.CreateContext();

            var CUT = new GetLevelQueryHandler(context);
            var command = new GetLevelQuery
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                UserId = 234081234
            };

            var response = await CUT.Handle(command, default);

            response.Success.Should().BeFalse();
            response.Message.Should().Be("That user could not be found.");
        }

        [Test]
        public async Task WhenCallingGetLevelQueryHandler_IfUserExists_ReturnResponseAsync()
        {
            var context = this.DatabaseFixture.CreateContext();

            var CUT = new GetLevelQueryHandler(context);
            var command = new GetLevelQuery
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                UserId = TestDatabaseFixture.Member1.UserId
            };

            var response = await CUT.Handle(command, default);

            response.Success.Should().BeTrue();
            response.UsersXp.Should().Be(0);
            response.UsersLevel.Should().Be(1);
            response.LevelProgress.Should().Be(0);
            response.XpForNextLevel.Should().Be(10);
            response.NextRoleRewardId.Should().Be(7);
            response.NextRewardLevel.Should().Be(10);
        }
    }
}

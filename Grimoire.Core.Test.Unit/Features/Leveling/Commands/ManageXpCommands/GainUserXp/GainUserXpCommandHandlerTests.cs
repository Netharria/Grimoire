// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Grimoire.Core.Features.Leveling.Commands.ManageXpCommands.GainUserXp;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Commands.ManageXpCommands.GainUserXp
{
    [TestFixture]
    public class GainUserXpCommandHandlerTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhenAwardUserXpCommandHandlerCalled_UpdateMemebersXpAsync()
        {
            var context = this.DatabaseFixture.CreateContext();

            var cut = new GainUserXpCommandHandler(context);
            context.Database.BeginTransaction();
            var result = await cut.Handle(
                new GainUserXpCommand
                {
                    UserId = TestDatabaseFixture.Member1.UserId,
                    GuildId = TestDatabaseFixture.Member1.GuildId,
                    ChannelId = TestDatabaseFixture.Channel1.Id,
                    RoleIds = new [] { TestDatabaseFixture.Role1.Id }
                }, default);
            context.ChangeTracker.Clear();
            result.Success.Should().BeTrue();
            result.EarnedRewards.Should().BeEmpty();
            result.PreviousLevel.Should().BeGreaterThan(0);
            result.CurrentLevel.Should().BeGreaterThan(0);
            result.LoggingChannel.Should().Be(TestDatabaseFixture.Guild2.LevelSettings.LevelChannelLogId);

            var member = await context.Members.Where(x =>
                x.UserId == TestDatabaseFixture.Member1.UserId
                && x.GuildId == TestDatabaseFixture.Member1.GuildId
                ).Include(x => x.XpHistory).FirstAsync();

            member.XpHistory.Sum(x => x.Xp).Should().BeGreaterThan(0);
        }

        [Test]
        public async Task WhenAwardUserXpCommandHandlerCalled_AndMemberInvalid_ReturnFalseResponseAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var cut = new GainUserXpCommandHandler(context);

            var result = await cut.Handle(
                new GainUserXpCommand
                {
                    UserId = TestDatabaseFixture.Member3.UserId,
                    GuildId = TestDatabaseFixture.Member3.GuildId,
                    ChannelId = TestDatabaseFixture.Channel1.Id,
                    RoleIds = new [] { TestDatabaseFixture.Role1.Id }
                }, default);
            context.ChangeTracker.Clear();
            result.Success.Should().BeFalse();
        }
    }
}

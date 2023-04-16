// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Commands.ManageXpCommands.AwardUserXp;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Commands.ManageXpCommands.AwardUserXp
{
    [TestFixture]
    public class AwardUserXpCommandHandlerTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhenAwardUserXpCommandHandlerCalled_UpdateMemebersXpAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var cut = new AwardUserXpCommandHandler(context);

            var result = await cut.Handle(
                new AwardUserXpCommand
                {
                    UserId = TestDatabaseFixture.Member1.UserId,
                    GuildId = TestDatabaseFixture.Member1.GuildId,
                    XpToAward = 20
                }, default);
            context.ChangeTracker.Clear();

            var member = await context.Members.Where(x =>
                x.UserId == TestDatabaseFixture.Member1.UserId
                && x.GuildId == TestDatabaseFixture.Member1.GuildId
                ).Include(x => x.XpHistory).FirstAsync();
            member.XpHistory.Sum(x => x.Xp).Should().Be(20);
        }

        [Test]
        public void WhenAwardUserXpCommandHandlerCalled_WithMissingUser_ReturnFailedResponse()
        {
            var context = this.DatabaseFixture.CreateContext();

            var cut = new AwardUserXpCommandHandler(context);
            context.Database.BeginTransaction();
            var response = Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(
                new AwardUserXpCommand
                {
                    UserId = 20001,
                    GuildId = TestDatabaseFixture.Member1.GuildId,
                    XpToAward = 20
                }, default));
            context.ChangeTracker.Clear();
            response.Should().NotBeNull();
            response?.Message.Should().Be("<@!20001> was not found. Have they been on the server before?");
        }
    }
}

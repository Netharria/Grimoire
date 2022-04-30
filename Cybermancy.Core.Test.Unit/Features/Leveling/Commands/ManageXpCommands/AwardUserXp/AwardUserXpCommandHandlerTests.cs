// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.AwardUserXp;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.Features.Leveling.Commands.ManageXpCommands.AwardUserXp
{
    [TestFixture]
    public class AwardUserXpCommandHandlerTests
    {
        [Test]
        public async Task WhenAwardUserXpCommandHandlerCalled_UpdateMemebersXpAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var cut = new AwardUserXpCommandHandler(context);

            var result = await cut.Handle(
                new AwardUserXpCommand
                {
                    UserId = TestCybermancyDbContextFactory.Member1.UserId,
                    GuildId = TestCybermancyDbContextFactory.Member1.GuildId,
                    XpToAward = 20
                }, default);

            result.Success.Should().BeTrue();

            var member = await context.Members.Where(x =>
                x.UserId == TestCybermancyDbContextFactory.Member1.UserId
                && x.GuildId == TestCybermancyDbContextFactory.Member1.GuildId
                ).FirstAsync();
            member.Xp.Should().Be(20);
        }

        [Test]
        public async Task WhenAwardUserXpCommandHandlerCalled_WithNegativeReward_ReturnFailedResponseAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var cut = new AwardUserXpCommandHandler(context);

            var result = await cut.Handle(
                new AwardUserXpCommand
                {
                    UserId = TestCybermancyDbContextFactory.Member1.UserId,
                    GuildId = TestCybermancyDbContextFactory.Member1.GuildId,
                    XpToAward = -20
                }, default);

            result.Success.Should().BeFalse();
            result.Message.Should().Be("Xp needs to be a positive value.");
        }

        [Test]
        public async Task WhenAwardUserXpCommandHandlerCalled_WithMissingUser_ReturnFailedResponseAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var cut = new AwardUserXpCommandHandler(context);

            var result = await cut.Handle(
                new AwardUserXpCommand
                {
                    UserId = 20001,
                    GuildId = TestCybermancyDbContextFactory.Member1.GuildId,
                    XpToAward = 20
                }, default);

            result.Success.Should().BeFalse();
            result.Message.Should().Be("<@!20001> was not found. Have they been on the server before?");
        }
    }
}

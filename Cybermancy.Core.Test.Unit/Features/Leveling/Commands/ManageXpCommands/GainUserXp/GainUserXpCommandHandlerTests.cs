// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.GainUserXp;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.Features.Leveling.Commands.ManageXpCommands.GainUserXp
{
    [TestFixture]
    public class GainUserXpCommandHandlerTests
    {
        [Test]
        public async Task WhenAwardUserXpCommandHandlerCalled_UpdateMemebersXpAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var cut = new GainUserXpCommandHandler(context);

            var result = await cut.Handle(
                new GainUserXpCommand
                {
                    UserId = TestCybermancyDbContextFactory.Member1.UserId,
                    GuildId = TestCybermancyDbContextFactory.Member1.GuildId,
                    ChannelId = TestCybermancyDbContextFactory.Channel.Id,
                    RoleIds = new [] { TestCybermancyDbContextFactory.Role1.Id }
                }, default);

            result.Success.Should().BeTrue();
            result.EarnedRewards.Should().BeEmpty();
            result.PreviousLevel.Should().BeGreaterThan(0);
            result.CurrentLevel.Should().BeGreaterThan(0);
            result.LoggingChannel.Should().Be(TestCybermancyDbContextFactory.Guild2.LevelSettings.LevelChannelLogId);

            var member = await context.Members.Where(x =>
                x.UserId == TestCybermancyDbContextFactory.Member1.UserId
                && x.GuildId == TestCybermancyDbContextFactory.Member1.GuildId
                ).FirstAsync();

            member.Xp.Should().BeGreaterThan(0);
        }

        [Test]
        public async Task WhenAwardUserXpCommandHandlerCalled_AndMemberInvalid_ReturnFalseResponseAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var cut = new GainUserXpCommandHandler(context);

            var result = await cut.Handle(
                new GainUserXpCommand
                {
                    UserId = TestCybermancyDbContextFactory.Member3.UserId,
                    GuildId = TestCybermancyDbContextFactory.Member3.GuildId,
                    ChannelId = TestCybermancyDbContextFactory.Channel.Id,
                    RoleIds = new [] { TestCybermancyDbContextFactory.Role1.Id }
                }, default);

            result.Success.Should().BeFalse();
        }
    }
}

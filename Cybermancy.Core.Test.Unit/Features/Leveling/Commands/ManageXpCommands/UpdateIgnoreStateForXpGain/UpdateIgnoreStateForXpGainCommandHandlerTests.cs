// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.UpdateIgnoreStateForXpGain;
using Cybermancy.Core.Features.Shared.SharedDtos;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.Features.Leveling.Commands.ManageXpCommands.UpdateIgnoreStateForXpGain
{
    [TestFixture]
    public class UpdateIgnoreStateForXpGainCommandHandlerTests
    {
        [Test]
        public async Task WhenUpdateIgnoreStateForXpGainCommandHandlerCalled_UpdateIgnoreStatusAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var cut = new UpdateIgnoreStateForXpGainCommandHandler(context);

            var result = await cut.Handle(
                new UpdateIgnoreStateForXpGainCommand
                {
                    Users = new [] { new UserDto { Id = TestCybermancyDbContextFactory.Member1.UserId } },
                    GuildId = TestCybermancyDbContextFactory.Member1.GuildId,
                    ShouldIgnore = true,
                    Channels = new []
                    {
                        new ChannelDto
                        {
                            Id = TestCybermancyDbContextFactory.Channel.Id,
                            GuildId = TestCybermancyDbContextFactory.Channel.GuildId
                        }
                    },
                    Roles = new []
                    {
                        new RoleDto
                        {
                            Id = TestCybermancyDbContextFactory.Role1.Id,
                            GuildId = TestCybermancyDbContextFactory.Role1.GuildId
                        }
                    }
                }, default);

            result.Success.Should().BeTrue();
            result.Message.Should().Be("<@!4> <@&6> <#3>  are now ignored for xp gain.");

            var member = await context.Members.Where(x =>
                x.UserId == TestCybermancyDbContextFactory.Member1.UserId
                && x.GuildId == TestCybermancyDbContextFactory.Member1.GuildId
                ).FirstAsync();

            member.IsXpIgnored.Should().BeTrue();

            var role = await context.Roles.Where(x =>
                x.Id == TestCybermancyDbContextFactory.Role1.Id
                && x.GuildId == TestCybermancyDbContextFactory.Role1.GuildId
                ).FirstAsync();

            role.IsXpIgnored.Should().BeTrue();

            var channel = await context.Channels.Where(x =>
                x.Id == TestCybermancyDbContextFactory.Channel.Id
                && x.GuildId == TestCybermancyDbContextFactory.Channel.GuildId
                ).FirstAsync();

            channel.IsXpIgnored.Should().BeTrue();
        }

        [Test]
        public async Task WhenUpdateIgnoreStateForXpGainCommandHandlerCalled_AndThereAreInvalidAndMissingIds_UpdateMessageAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var cut = new UpdateIgnoreStateForXpGainCommandHandler(context);

            var result = await cut.Handle(
                new UpdateIgnoreStateForXpGainCommand
                {
                    GuildId = TestCybermancyDbContextFactory.Member1.GuildId,
                    ShouldIgnore = true,
                    InvalidIds = new [] { "asldfkja" }
                }, default);

            result.Success.Should().BeTrue();
            result.Message.Should().Be("Could not match asldfkja with a role, channel or user. ");
        }
    }
}

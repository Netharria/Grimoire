// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Commands.ManageXpCommands.ReclaimUserXp;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Commands.ManageXpCommands.ReclaimUserXp;

public class ReclaimUserXpCommandHandlerTests
{

    [Test]
    public async Task WhenReclaimUserXpCommandHandlerCalled_UpdateMemebersXpAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var cut = new ReclaimUserXpCommandHandler(context);

        var result = await cut.Handle(
            new ReclaimUserXpCommand
            {
                UserId = TestDatabaseFixture.Member1.UserId,
                GuildId = TestDatabaseFixture.Member1.GuildId,
                XpToTake = 400,
                XpOption = XpOption.Amount
            }, default);
        context.ChangeTracker.Clear();

        var member = await context.Members.Where(x =>
            x.UserId == TestDatabaseFixture.Member1.UserId
            && x.GuildId == TestDatabaseFixture.Member1.GuildId
            ).FirstAsync();

        member.XpHistory.Sum(x => x.Xp).Should().Be(0);
    }

    [Test]
    public async Task WhenReclaimUserXpCommandHandlerCalled_WithAllArgument_UpdateMemebersXpAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var cut = new ReclaimUserXpCommandHandler(context);

        var result = await cut.Handle(
            new ReclaimUserXpCommand
            {
                UserId = TestDatabaseFixture.Member1.UserId,
                GuildId = TestDatabaseFixture.Member1.GuildId,
                XpToTake = 0,
                XpOption = XpOption.All
            }, default);
        context.ChangeTracker.Clear();

        var member = await context.Members.Where(x =>
            x.UserId == TestDatabaseFixture.Member1.UserId
            && x.GuildId == TestDatabaseFixture.Member1.GuildId
            ).FirstAsync();

        member.XpHistory.Sum(x => x.Xp).Should().Be(0);
    }

    [Test]
    public void WhenReclaimUserXpCommandHandlerCalled_WithMissingUser_ReturnFailedResponse()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var cut = new ReclaimUserXpCommandHandler(context);

        var response = Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(
            new ReclaimUserXpCommand
            {
                UserId = 20001,
                GuildId = TestDatabaseFixture.Member1.GuildId,
                XpToTake = 20,
                XpOption = XpOption.Amount
            }, default));
        context.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response?.Message.Should().Be("<@!20001> was not found. Have they been on the server before?");
    }
}

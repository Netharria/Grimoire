// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Queries.GetLevel;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Queries.GetLevel;

[TestFixture]
public class GetLevelQueryHandlerTests
{

    [Test]
    public void WhenCallingGetLevelQueryHandler_IfUserDoesNotExist_ReturnFailedResponse()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();

        var CUT = new GetLevelQueryHandler(context);
        var command = new GetLevelQuery
        {
            GuildId = TestDatabaseFixture.Guild1.Id,
            UserId = 234081234
        };

        var response = Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("That user could not be found.");
    }

    [Test]
    public async Task WhenCallingGetLevelQueryHandler_IfUserExists_ReturnResponseAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();

        var CUT = new GetLevelQueryHandler(context);
        var command = new GetLevelQuery
        {
            GuildId = TestDatabaseFixture.Guild1.Id,
            UserId = TestDatabaseFixture.Member1.UserId
        };

        var response = await CUT.Handle(command, default);

        response.UsersXp.Should().Be(0);
        response.UsersLevel.Should().Be(1);
        response.LevelProgress.Should().Be(0);
        response.XpForNextLevel.Should().Be(10);
        response.NextRoleRewardId.Should().Be(7);
        response.NextRewardLevel.Should().Be(10);
    }
}

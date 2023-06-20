// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Queries.GetLeaderboard;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Queries.GetLeaderboard;

[TestFixture]
public class GetLeaderboardQueryHandlerTests
{

    [Test]
    public void WhenCallingGetLeaderboardQueryHandler_IfProvidedUserNotFound_FailResponse()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();

        var CUT = new GetLeaderboardQueryHandler(context);
        var command = new GetLeaderboardQuery
        {
            GuildId = TestDatabaseFixture.Guild1.Id,
            UserId = 234081234
        };

        var response = Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("Could not find user on leaderboard.");
    }

    [Test]
    public async Task WhenCallingGetLeaderboardQueryHandler_ReturnLeaderboardAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();

        var CUT = new GetLeaderboardQueryHandler(context);
        var command = new GetLeaderboardQuery
        {
            GuildId = TestDatabaseFixture.Guild1.Id
        };

        var response = await CUT.Handle(command, default);

        response.LeaderboardText.Should().Be("**1** <@!4> **XP:** 0\n**2** <@!5> **XP:** 0\n");
        response.TotalUserCount.Should().Be(2);
    }
}

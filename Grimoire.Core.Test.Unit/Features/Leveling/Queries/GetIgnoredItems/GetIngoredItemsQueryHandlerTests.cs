// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Queries.GetIgnoredItems;
using Grimoire.Domain;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Queries.GetIgnoredItems;

[TestFixture]
public class GetIngoredItemsQueryHandlerTests
{

    [Test]
    public async Task WhenCallingGetIgnoredItemsHandler_IfNoIgnoredItems_ReturnFailedResponseAsync()
    {
        var context = TestDatabaseFixture.CreateContext();

        context.Guilds.Add(new Guild { Id = 34958734 });
        await context.SaveChangesAsync();

        var CUT = new GetIgnoredItemsQueryHandler(context);
        var command = new GetIgnoredItemsQuery
        {
            GuildId = 34958734
        };

        var response = Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("This server does not have any ignored channels, roles or users.");
    }

    [Test]
    public async Task WhenCallingGetIgnoredItemsHandler_IfIgnoredItems_ReturnSuccessResponseAsync()
    {
        var context = TestDatabaseFixture.CreateContext();

        var CUT = new GetIgnoredItemsQueryHandler(context);
        var command = new GetIgnoredItemsQuery
        {
            GuildId = TestDatabaseFixture.Guild1.Id
        };

        var response = await CUT.Handle(command, default);

        response.Message.Should().Be("**Channels**\n<#12>\n\n**Roles**\n<@&7>\n\n**Users**\n");
    }
}

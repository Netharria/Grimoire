// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Cybermancy.Core.Exceptions;
using Cybermancy.Core.Features.Leveling.Queries.GetIgnoredItems;
using Cybermancy.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.Features.Leveling.Queries.GetIgnoredItems
{
    [TestFixture]
    public class GetIngoredItemsQueryHandlerTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhenCallingGetIgnoredItemsHandler_IfNoIgnoredItems_ReturnFailedResponseAsync()
        {
            var context = this.DatabaseFixture.CreateContext();

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
            var context = this.DatabaseFixture.CreateContext();

            var CUT = new GetIgnoredItemsQueryHandler(context);
            var command = new GetIgnoredItemsQuery
            {
                GuildId = TestDatabaseFixture.Guild1.Id
            };

            var response = await CUT.Handle(command, default);

            response.Message.Should().Be("**Channels**\n<#12>\n\n**Roles**\n<@&7>\n\n**Users**\n");
        }
    }
}

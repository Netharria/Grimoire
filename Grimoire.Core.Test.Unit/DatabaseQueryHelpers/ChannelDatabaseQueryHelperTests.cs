// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.Shared.SharedDtos;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class ChannelDatabaseQueryHelperTests
    {

        [Test]
        public async Task WhenChannelsAreNotInDatabase_AddThemAsync()
        {
            var context = TestDatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var channelsToAdd = new List<ChannelDto>
            {
                new ChannelDto() { Id = 2, GuildId = TestDatabaseFixture.Guild1.Id},
                new ChannelDto() { Id = 3, GuildId = TestDatabaseFixture.Guild1.Id},
                new ChannelDto() { Id = 4, GuildId = TestDatabaseFixture.Guild1.Id},
                new ChannelDto() { Id = 5, GuildId = TestDatabaseFixture.Guild1.Id}
            };
            var result = await context.Channels.AddMissingChannelsAsync(channelsToAdd, default);

            await context.SaveChangesAsync();

            result.Should().BeTrue();
            context.Channels.Should().HaveCount(5);
        }
    }
}

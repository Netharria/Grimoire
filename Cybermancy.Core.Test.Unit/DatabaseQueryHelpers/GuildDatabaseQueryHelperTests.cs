// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Features.Shared.SharedDtos;
using FluentAssertions;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class GuildDatabaseQueryHelperTests
    {
        [Test]
        public async Task WhenChannelsAreNotInDatabase_AddThemAsync()
        {
            var context = TestCybermancyDbContextFactory.Create();
            var guildsToAdd = new List<GuildDto>
            {
                new GuildDto() { Id = 1 },
                new GuildDto() { Id = 2 },
                new GuildDto() { Id = 3 }
            };
            var result = await context.Guilds.AddMissingGuildsAsync(guildsToAdd, default);

            await context.SaveChangesAsync();

            result.Should().BeTrue();
            context.Guilds.Should().HaveCount(3);
        }
    }
}

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
    public class GuildDatabaseQueryHelperTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhenChannelsAreNotInDatabase_AddThemAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var guildsToAdd = new List<GuildDto>
            {
                new GuildDto() { Id = 1 },
                new GuildDto() { Id = 2 },
                new GuildDto() { Id = 3 }
            };
            var result = await context.Guilds.AddMissingGuildsAsync(guildsToAdd, default);

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            result.Should().BeTrue();
            context.Guilds.Should().HaveCount(3);
        }
    }
}

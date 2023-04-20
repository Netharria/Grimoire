// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.DatabaseQueryHelpers;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class IMemberDatabaseQueryHelperTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhereMembersHaveIds_WhenProvidedValidIds_ReturnsResultAsync()
        {
            var context = this.DatabaseFixture.CreateContext();

            var result = await context.Members.WhereMembersHaveIds(new ulong[]{
                TestDatabaseFixture.Member1.UserId },
                TestDatabaseFixture.Member1.GuildId).ToArrayAsync();

            result.Should().HaveCount(1);
            result.Should().AllSatisfy(x => x.UserId.Should().Be(TestDatabaseFixture.Member1.UserId))
                .And.AllSatisfy(x => x.GuildId.Should().Be(TestDatabaseFixture.Member1.GuildId));
        }

        [Test]
        public async Task WWhereMemberHasId_WhenProvidedValidId_ReturnsResultAsync()
        {
            var context = this.DatabaseFixture.CreateContext();

            var result = await context.Members.WhereMemberHasId(
                TestDatabaseFixture.Member2.UserId,
                TestDatabaseFixture.Member2.GuildId).ToArrayAsync();

            result.Should().HaveCount(1);
            result.Should().AllSatisfy(x => x.UserId.Should().Be(TestDatabaseFixture.Member2.UserId))
                .And.AllSatisfy(x => x.GuildId.Should().Be(TestDatabaseFixture.Member2.GuildId));
        }
    }
}

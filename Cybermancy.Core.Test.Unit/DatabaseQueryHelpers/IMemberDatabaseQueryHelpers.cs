// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.DatabaseQueryHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class IMemberDatabaseQueryHelpers
    {
        [Test]
        public async Task WhereMembersHaveIds_WhenProvidedValidIds_ReturnsResultAsync()
        {
            var context = TestCybermancyDbContextFactory.Create();

            var result = await context.Members.WhereMembersHaveIds(new ulong[]{
                TestCybermancyDbContextFactory.Member1.UserId },
                TestCybermancyDbContextFactory.Member1.GuildId).ToArrayAsync();

            result.Should().HaveCount(1);
            result.Should().Contain(TestCybermancyDbContextFactory.Member1);
        }

        [Test]
        public async Task WWhereMemberHasId_WhenProvidedValidId_ReturnsResultAsync()
        {
            var context = TestCybermancyDbContextFactory.Create();

            var result = await context.Members.WhereMemberHasId(
                TestCybermancyDbContextFactory.Member2.UserId,
                TestCybermancyDbContextFactory.Member2.GuildId).ToArrayAsync();

            result.Should().HaveCount(1);
            result.Should().Contain(TestCybermancyDbContextFactory.Member2);
        }
    }
}

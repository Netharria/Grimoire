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
    public class IIdentifiableDatabaseQueryHelperTests
    {
        [Test]
        public async Task WhereIdsAre_WhenProvidedValidIds_ReturnsResultAsync()
        {
            var context = TestCybermancyDbContextFactory.Create();

            var result = await context.Guilds.WhereIdsAre(new ulong[]{ TestCybermancyDbContextFactory.Guild1.Id }).ToArrayAsync();

            result.Should().HaveCount(1);
            result.Should().Contain(TestCybermancyDbContextFactory.Guild1);
        }

        [Test]
        public async Task WhereIdIs_WhenProvidedValidId_ReturnsResultAsync()
        {
            var context = TestCybermancyDbContextFactory.Create();

            var result = await context.Guilds.WhereIdIs(TestCybermancyDbContextFactory.Guild2.Id).ToArrayAsync();

            result.Should().HaveCount(1);
            result.Should().Contain(TestCybermancyDbContextFactory.Guild2);
        }
    }
}

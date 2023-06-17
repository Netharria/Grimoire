// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.DatabaseQueryHelpers;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class IXpIgnoreDatabaseQueryHelperTests
    {

        [Test]
        public async Task WhenWhereIgnoredCalled_ReturnAllIgnoredItemsAsync()
        {
            var context = TestDatabaseFixture.CreateContext();

            var result = await context.Members.WhereIgnored().ToListAsync();

            result.Should().NotBeEmpty().And.AllSatisfy(x => x.IsXpIgnored.Should().BeTrue());
        }

        [Test]
        public async Task WhenWhereIgnoredCalled_WithFalseParameter_ReturnAllNotIgnoredItemsAsync()
        {
            var context = TestDatabaseFixture.CreateContext();

            var result = await context.Members.WhereIgnored(false).ToListAsync();

            result.Should().NotBeEmpty().And.AllSatisfy(x => x.IsXpIgnored.Should().BeFalse());
        }
    }
}

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Cybermancy.Core.DatabaseQueryHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class IXpIgnoreDatabaseQueryHelpers
    {
        [Test]
        public async Task WhenWhereIgnoredCalled_ReturnAllIgnoredItemsAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var result = context.Members.WhereIgnored();

            result.Should().NotBeEmpty().And.AllSatisfy(x => x.IsXpIgnored.Should().BeTrue());
        }

        [Test]
        public async Task WhenWhereIgnoredCalled_WithFalseParameter_ReturnAllNotIgnoredItemsAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var result = context.Members.WhereIgnored(false);

            result.Should().NotBeEmpty().And.AllSatisfy(x => x.IsXpIgnored.Should().BeFalse());
        }
    }
}

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public void WhenWhereIgnoredCalled_ReturnAllIgnoredItems()
        {
            var context = TestCybermancyDbContextFactory.Create();
            var result = context.Members.WhereIgnored();

            result.Should().NotBeEmpty().And.AllSatisfy(x => x.IsXpIgnored.Should().BeTrue());
        }

        [Test]
        public void WhenWhereIgnoredCalled_WithFalseParameter_ReturnAllNotIgnoredItems()
        {
            var context = TestCybermancyDbContextFactory.Create();
            var result = context.Members.WhereIgnored(false);

            result.Should().NotBeEmpty().And.AllSatisfy(x => x.IsXpIgnored.Should().BeFalse());
        }
    }
}

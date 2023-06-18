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

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers;

[TestFixture]
public class IIdentifiableDatabaseQueryHelperTests
{

    [Test]
    public async Task WhereIdsAre_WhenProvidedValidIds_ReturnsResultAsync()
    {
        var context = TestDatabaseFixture.CreateContext();

        var result = await context.Guilds.WhereIdsAre(new ulong[]{ TestDatabaseFixture.Guild1.Id }).ToArrayAsync();

        result.Should().HaveCount(1);
        result.Should().AllSatisfy(x => x.Id.Should().Be(TestDatabaseFixture.Guild1.Id));
    }

    [Test]
    public async Task WhereIdIs_WhenProvidedValidId_ReturnsResultAsync()
    {
        var context = TestDatabaseFixture.CreateContext();

        var result = await context.Guilds.WhereIdIs(TestDatabaseFixture.Guild2.Id).ToArrayAsync();

        result.Should().HaveCount(1);
        result.Should().AllSatisfy(x => x.Id.Should().Be(TestDatabaseFixture.Guild2.Id));
    }
}

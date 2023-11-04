// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.Shared.SharedDtos;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers;

[TestFixture]
public class MemberDatabaseQueryHelperTests
{

    [Test]
    public async Task WhenMembersAreNotInDatabase_AddThemAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var membersToAdd = new List<MemberDto>
        {
            new MemberDto() { UserId = TestDatabaseFixture.User1.Id, GuildId = TestDatabaseFixture.Guild2.Id},
            new MemberDto() { UserId = TestDatabaseFixture.User2.Id, GuildId = TestDatabaseFixture.Guild2.Id}
        };
        var result = await context.Members.AddMissingMembersAsync(membersToAdd, default);

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        result.Should().BeTrue();
        context.Members.Where(x => x.GuildId == TestDatabaseFixture.Guild2.Id).Should().HaveCount(2);
    }

    [Test]
    public async Task WhenWhereLoggingEnabledCalled_GetMembersInGuildsWhereLoggingIsEnabledAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();

        var result = await context.Members.WhereLoggingEnabled().ToArrayAsync();

        result.Should().AllSatisfy(x => x.Guild?.UserLogSettings?.ModuleEnabled.Should().BeTrue());
    }

    [Test]
    public async Task WhenWhereLevelingEnabledCalled_GetMembersInGuildsWhereLevelingIsEnabledAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();

        var result = await context.Members.WhereLevelingEnabled().ToArrayAsync();

        result.Should().AllSatisfy(x => x.Guild?.LevelSettings?.ModuleEnabled.Should().BeTrue());
    }

    [Test]
    public async Task WhenWhereMemberNotIgnoredCalled_GetMembersThatArentIgnoredAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();

        var result = await context.Members.WhereMemberNotIgnored(
            TestDatabaseFixture.Channel1.Id,
            new ulong[]
            {
                TestDatabaseFixture.Role1.Id
            }).ToArrayAsync();

        result.Should().AllSatisfy(x => x.IsIgnoredMember.Should().NotBeNull());
    }
}

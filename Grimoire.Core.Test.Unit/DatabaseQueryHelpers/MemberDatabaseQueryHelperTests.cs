// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.Shared.SharedDtos;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class MemberDatabaseQueryHelperTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhenMembersAreNotInDatabase_AddThemAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
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
            var context = this.DatabaseFixture.CreateContext();

            var result = await context.Members.WhereLoggingEnabled().ToArrayAsync();

            result.Should().AllSatisfy(x => x.Guild?.LogSettings?.ModuleEnabled.Should().BeTrue());
        }

        [Test]
        public async Task WhenWhereLevelingEnabledCalled_GetMembersInGuildsWhereLevelingIsEnabledAsync()
        {
            var context = this.DatabaseFixture.CreateContext();

            var result = await context.Members.WhereLevelingEnabled().ToArrayAsync();

            result.Should().AllSatisfy(x => x.Guild?.LevelSettings?.ModuleEnabled.Should().BeTrue());
        }

        [Test]
        public async Task WhenWhereMemberNotIgnoredCalled_GetMembersThatArentIgnoredAsync()
        {
            var context = this.DatabaseFixture.CreateContext();

            var result = await context.Members.WhereMemberNotIgnored(
                TestDatabaseFixture.Channel1.Id,
                new ulong[]
                {
                    TestDatabaseFixture.Role1.Id,
                    TestDatabaseFixture.Role2.Id
                }).ToArrayAsync();

            result.Should().AllSatisfy(x => x.IsXpIgnored.Should().BeFalse());
        }
    }
}

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Features.Shared.SharedDtos;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class MemberDatabaseQueryHelperTests
    {
        [Test]
        public async Task WhenMembersAreNotInDatabase_AddThemAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var membersToAdd = new List<MemberDto>
            {
                new MemberDto() { UserId = TestCybermancyDbContextFactory.User1.Id, GuildId = TestCybermancyDbContextFactory.Guild2.Id},
                new MemberDto() { UserId = TestCybermancyDbContextFactory.User2.Id, GuildId = TestCybermancyDbContextFactory.Guild2.Id}
            };
            var result = await context.Members.AddMissingMembersAsync(membersToAdd, default);

            await context.SaveChangesAsync();

            result.Should().BeTrue();
            context.Members.Where(x => x.GuildId == TestCybermancyDbContextFactory.Guild2.Id).Should().HaveCount(2);
        }

        [Test]
        public async Task WhenWhereLoggingEnabledCalled_GetMembersInGuildsWhereLoggingIsEnabledAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var result = await context.Members.WhereLoggingEnabled().ToArrayAsync();

            result.Should().Contain(TestCybermancyDbContextFactory.Member1)
                .And.Contain(TestCybermancyDbContextFactory.Member2);
        }

        [Test]
        public async Task WhenWhereLevelingEnabledCalled_GetMembersInGuildsWhereLevelingIsEnabledAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var result = await context.Members.WhereLevelingEnabled().ToArrayAsync();

            result.Should().Contain(TestCybermancyDbContextFactory.Member1)
                .And.Contain(TestCybermancyDbContextFactory.Member2);
        }

        [Test]
        public async Task WhenWhereMemberNotIgnoredCalled_GetMembersThatArentIgnoredAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var result = await context.Members.WhereMemberNotIgnored(
                TestCybermancyDbContextFactory.Channel.Id,
                new ulong[]
                {
                    TestCybermancyDbContextFactory.Role1.Id,
                    TestCybermancyDbContextFactory.Role2.Id
                }).ToArrayAsync();

            result.Should().Contain(TestCybermancyDbContextFactory.Member1)
                .And.Contain(TestCybermancyDbContextFactory.Member2)
                .And.NotContain(TestCybermancyDbContextFactory.Member3);
        }
    }
}

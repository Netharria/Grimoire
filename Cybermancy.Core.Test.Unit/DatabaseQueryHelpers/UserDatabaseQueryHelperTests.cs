// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Features.Shared.SharedDtos;
using FluentAssertions;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class UserDatabaseQueryHelperTests
    {
        [Test]
        public async Task WhenUsersAreNotInDatabase_AddThemAsync()
        {
            var context = await TestCybermancyDbContextFactory.CreateAsync();

            var usersToAdd = new List<UserDto>
            {
                new UserDto() { Id = TestCybermancyDbContextFactory.User1.Id },
                new UserDto() { Id = TestCybermancyDbContextFactory.User2.Id },
                new UserDto() { Id = 45 }
            };
            var result = await context.Users.AddMissingUsersAsync(usersToAdd, default);

            await context.SaveChangesAsync();

            result.Should().BeTrue();
            context.Users.Should().HaveCount(3);
        }
    }
}

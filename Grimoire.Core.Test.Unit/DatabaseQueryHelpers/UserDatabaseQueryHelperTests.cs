// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.Shared.SharedDtos;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers;

[TestFixture]
public class UserDatabaseQueryHelperTests
{

    [Test]
    public async Task WhenUsersAreNotInDatabase_AddThemAsync()
    {
        var context = TestDatabaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var usersToAdd = new List<UserDto>
        {
            new UserDto() { Id = TestDatabaseFixture.User1.Id },
            new UserDto() { Id = TestDatabaseFixture.User2.Id },
            new UserDto() { Id = 45 }
        };
        var result = await context.Users.AddMissingUsersAsync(usersToAdd, default);

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        result.Should().BeTrue();
        context.Users.Should().HaveCount(3);
    }
}

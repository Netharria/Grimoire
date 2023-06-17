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

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class RoleDatabaseQueryHelperTests
    {

        [Test]
        public async Task WhenRolesAreNotInDatabase_AddThemAsync()
        {
            var context = TestDatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var rolesToAdd = new List<RoleDto>
            {
                new RoleDto() { Id = TestDatabaseFixture.Role1.Id, GuildId = TestDatabaseFixture.Guild1.Id },
                new RoleDto() { Id = TestDatabaseFixture.Role2.Id, GuildId = TestDatabaseFixture.Guild1.Id },
                new RoleDto() { Id = 4, GuildId = TestDatabaseFixture.Guild1.Id },
                new RoleDto() { Id = 5, GuildId = TestDatabaseFixture.Guild1.Id }
            };
            var result = await context.Roles.AddMissingRolesAsync(rolesToAdd, default);

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            result.Should().BeTrue();
            context.Roles.Should().HaveCount(4);
        }
    }
}

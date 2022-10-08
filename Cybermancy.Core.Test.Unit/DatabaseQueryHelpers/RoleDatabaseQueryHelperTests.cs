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
    public class RoleDatabaseQueryHelperTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhenRolesAreNotInDatabase_AddThemAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
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

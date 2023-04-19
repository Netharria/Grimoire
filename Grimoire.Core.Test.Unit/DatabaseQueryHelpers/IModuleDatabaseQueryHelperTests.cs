// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Enums;
using Grimoire.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class IModuleDatabaseQueryHelperTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhenGetModulesOfTypeCalled_ReturnCorrectTypeofModuleAsync()
        {
            var context = this.DatabaseFixture.CreateContext();

            var levelingModule = await context.Guilds.GetModulesOfType(Module.Leveling)
                .OfType<GuildLevelSettings>()
                .ToListAsync();
            levelingModule.Should().NotBeEmpty();

            var loggingModule = await context.Guilds.GetModulesOfType(Module.UserLog)
                .OfType<GuildUserLogSettings>()
                .ToListAsync();
            loggingModule.Should().NotBeEmpty();

            var moderationModule = await context.Guilds.GetModulesOfType(Module.Moderation)
                .OfType<GuildModerationSettings>()
                .ToListAsync();
            moderationModule.Should().NotBeEmpty();
        }
    }
}

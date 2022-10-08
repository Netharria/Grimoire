// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Features.Leveling.Commands.SetLevelSettings;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.Features.Leveling.Commands.SetLevelSettings
{
    [TestFixture]
    public class SetLevelingSettingsCommandHandlerTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhenUpdatingGuildLevelSettings_IfGuildDoesNotExist_FailResponseAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = 12341234
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Could not find guild level settings.");
        }

        [Test]
        public async Task WhenUpdatingTextTime_IfNumberIsInvalid_FailResponseAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.TextTime,
                Value = "adsfas"
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Please give a valid number for TextTime.");
        }

        [Test]
        public async Task WhenUpdatingBase_IfNumberIsInvalid_FailResponseAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.Base,
                Value = "adsfas"
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Please give a valid number for base XP.");
        }

        [Test]
        public async Task WhenUpdatingModifier_IfNumberIsInvalid_FailResponseAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.Modifier,
                Value = "adsfas"
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Please give a valid number for Modifier.");
        }

        [Test]
        public async Task WhenUpdatingAmount_IfNumberIsInvalid_FailResponseAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.Amount,
                Value = "adsfas"
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Please give a valid number for Amount.");
        }

        [Test]
        public async Task WhenUpdatingLogChannel_IfNumberIsInvalid_FailResponseAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.LogChannel,
                Value = "345"
            };
            context.ChangeTracker.Clear();
            var response = await CUT.Handle(command, default);

            response.Success.Should().BeFalse();
            response.Message.Should().Be("Please give a valid channel for Log Channel.");
        }

        [Test]
        public async Task WhenUpdatingTextTime_IfNumberIsValid_UpdateSettingAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.TextTime,
                Value = "23"
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeTrue();
            var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

            guildSettings.TextTime.Should().Be(TimeSpan.FromMinutes(23));
        }

        [Test]
        public async Task WhenUpdatingBase_IfNumberIsValid_UpdateSettingAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.Base,
                Value = "23"
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeTrue();
            var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

            guildSettings.Base.Should().Be(23);
        }

        [Test]
        public async Task WhenUpdatingModifier_IfNumberIsValid_UpdateSettingAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.Modifier,
                Value = "23"
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeTrue();
            var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

            guildSettings.Modifier.Should().Be(23);
        }

        [Test]
        public async Task WhenUpdatingAmount_IfNumberIsValid_UpdateSettingAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.Amount,
                Value = "23"
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeTrue();
            var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

            guildSettings.Amount.Should().Be(23);
        }

        [Test]
        public async Task WhenUpdatingLogChannel_IfNumberIsValid_UpdateSettingAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.LogChannel,
                Value = "12345678901234567"
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeTrue();
            var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

            guildSettings.LevelChannelLogId.Should().Be(12345678901234567);
        }

        [Test]
        public async Task WhenUpdatingLogChannel_IfNumberIs0_UpdateSettingToNullAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var CUT = new SetLevelSettingsCommandHandler(context);
            var command = new SetLevelSettingsCommand
            {
                GuildId = TestDatabaseFixture.Guild1.Id,
                LevelSettings = LevelSettings.LogChannel,
                Value = "0"
            };

            var response = await CUT.Handle(command, default);
            context.ChangeTracker.Clear();
            response.Success.Should().BeTrue();
            var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

            guildSettings.LevelChannelLogId.Should().BeNull();
        }
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Commands.SetLevelSettings;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Commands.SetLevelSettings;

[TestFixture]
public class SetLevelingSettingsCommandHandlerTests
{

    [Test]
    public void WhenUpdatingGuildLevelSettings_IfGuildDoesNotExist_FailResponse()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var CUT = new SetLevelSettingsCommandHandler(context);
        var command = new SetLevelSettingsCommand
        {
            GuildId = 12341234
        };

        var response = Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));
        context.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response?.Message.Should().Be("Could not find guild level settings.");
    }

    [Test]
    public void WhenUpdatingTextTime_IfNumberIsInvalid_FailResponse()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var CUT = new SetLevelSettingsCommandHandler(context);
        var command = new SetLevelSettingsCommand
        {
            GuildId = TestDatabaseFixture.Guild1.Id,
            LevelSettings = LevelSettings.TextTime,
            Value = "adsfas"
        };

        var response = Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));
        context.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response?.Message.Should().Be("Please give a valid number for TextTime.");
    }

    [Test]
    public void WhenUpdatingBase_IfNumberIsInvalid_FailResponse()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var CUT = new SetLevelSettingsCommandHandler(context);
        var command = new SetLevelSettingsCommand
        {
            GuildId = TestDatabaseFixture.Guild1.Id,
            LevelSettings = LevelSettings.Base,
            Value = "adsfas"
        };

        var response = Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));
        context.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response?.Message.Should().Be("Please give a valid number for base XP.");
    }

    [Test]
    public void WhenUpdatingModifier_IfNumberIsInvalid_FailResponse()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var CUT = new SetLevelSettingsCommandHandler(context);
        var command = new SetLevelSettingsCommand
        {
            GuildId = TestDatabaseFixture.Guild1.Id,
            LevelSettings = LevelSettings.Modifier,
            Value = "adsfas"
        };

        var response = Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));
        context.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response?.Message.Should().Be("Please give a valid number for Modifier.");
    }

    [Test]
    public void WhenUpdatingAmount_IfNumberIsInvalid_FailResponse()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var CUT = new SetLevelSettingsCommandHandler(context);
        var command = new SetLevelSettingsCommand
        {
            GuildId = TestDatabaseFixture.Guild1.Id,
            LevelSettings = LevelSettings.Amount,
            Value = "adsfas"
        };

        var response = Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));
        context.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response?.Message.Should().Be("Please give a valid number for Amount.");
    }

    [Test]
    public void WhenUpdatingLogChannel_IfNumberIsInvalid_FailResponse()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var CUT = new SetLevelSettingsCommandHandler(context);
        var command = new SetLevelSettingsCommand
        {
            GuildId = TestDatabaseFixture.Guild1.Id,
            LevelSettings = LevelSettings.LogChannel,
            Value = "Something"
        };
        context.ChangeTracker.Clear();
        var response = Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("Please give a valid channel for Log Channel.");
    }

    [Test]
    public async Task WhenUpdatingTextTime_IfNumberIsValid_UpdateSettingAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
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
        var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

        guildSettings.TextTime.Should().Be(TimeSpan.FromMinutes(23));
    }

    [Test]
    public async Task WhenUpdatingBase_IfNumberIsValid_UpdateSettingAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
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
        var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

        guildSettings.Base.Should().Be(23);
    }

    [Test]
    public async Task WhenUpdatingModifier_IfNumberIsValid_UpdateSettingAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
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
        var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

        guildSettings.Modifier.Should().Be(23);
    }

    [Test]
    public async Task WhenUpdatingAmount_IfNumberIsValid_UpdateSettingAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
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
        var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

        guildSettings.Amount.Should().Be(23);
    }

    [Test]
    public async Task WhenUpdatingLogChannel_IfNumberIsValid_UpdateSettingAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
        context.Database.BeginTransaction();
        var CUT = new SetLevelSettingsCommandHandler(context);
        var command = new SetLevelSettingsCommand
        {
            GuildId = TestDatabaseFixture.Guild1.Id,
            LevelSettings = LevelSettings.LogChannel,
            Value = TestDatabaseFixture.Channel2.Id.ToString()
        };

        var response = await CUT.Handle(command, default);
        context.ChangeTracker.Clear();
        var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

        guildSettings.LevelChannelLogId.Should().Be(12);
    }

    [Test]
    public async Task WhenUpdatingLogChannel_IfNumberIs0_UpdateSettingToNullAsync()
    {
        var databaseFixture = new TestDatabaseFixture();
        using var context = databaseFixture.CreateContext();
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
        var guildSettings = await context.GuildLevelSettings.FirstAsync(x => x.GuildId == TestDatabaseFixture.Guild1.Id);

        guildSettings.LevelChannelLogId.Should().BeNull();
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using EntityFramework.Exceptions.PostgreSQL;
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Exceptions;
using Grimoire.Features.Leveling.Settings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Settings;

[Collection("Test collection")]
public sealed class SetLevelingSettingsCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong ChannelId = 1;

    private readonly Func<GrimoireDbContext> _createDbContext = () => new GrimoireDbContext(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .UseExceptionProcessor()
            .Options);

    private readonly IDbContextFactory<GrimoireDbContext> _mockDbContextFactory =
        Substitute.For<IDbContextFactory<GrimoireDbContext>>();

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Guild
        {
            Id = GuildId, LevelSettings = new GuildLevelSettings { GuildId = GuildId }
        });
        await dbContext.AddAsync(new Channel { Id = ChannelId, GuildId = GuildId });
        await dbContext.SaveChangesAsync();

        var guild = await dbContext.Guilds.FirstAsync(x => x.Id == GuildId);
        guild.ModChannelLog = ChannelId;

        await dbContext.SaveChangesAsync();

        this._mockDbContextFactory.CreateDbContextAsync().Returns(this._createDbContext());
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenUpdatingGuildLevelSettings_IfGuildDoesNotExist_FailResponse()
    {
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = 12341234, LevelSettings = LevelSettingsCommandGroup.LevelSettings.TextTime, Value = "something"
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(command, default));
        response.Should().NotBeNull();
        response.Message.Should().Be("Could not find guild level settings.");
    }

    [Fact]
    public async Task WhenUpdatingTextTime_IfNumberIsInvalid_FailResponse()
    {
        await using var dbContext = this._createDbContext();
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.TextTime, Value = "adsfas"
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(command, default));
        dbContext.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response.Message.Should().Be("Please give a valid number for TextTime.");
    }

    [Fact]
    public async Task WhenUpdatingBase_IfNumberIsInvalid_FailResponse()
    {
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.Base, Value = "adsfas"
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(command, default));
        response.Should().NotBeNull();
        response.Message.Should().Be("Please give a valid number for base XP.");
    }

    [Fact]
    public async Task WhenUpdatingModifier_IfNumberIsInvalid_FailResponse()
    {
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.Modifier, Value = "adsfas"
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(command, default));
        response.Should().NotBeNull();
        response.Message.Should().Be("Please give a valid number for Modifier.");
    }

    [Fact]
    public async Task WhenUpdatingAmount_IfNumberIsInvalid_FailResponse()
    {
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.Amount, Value = "adsfas"
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(command, default));
        response.Should().NotBeNull();
        response.Message.Should().Be("Please give a valid number for Amount.");
    }

    [Fact]
    public async Task WhenUpdatingLogChannel_IfNumberIsInvalid_FailResponse()
    {
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.LogChannel, Value = "Something"
        };
        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(command, default));

        response.Should().NotBeNull();
        response.Message.Should().Be("Please give a valid channel for Log Channel.");
    }

    [Fact]
    public async Task WhenUpdatingTextTime_IfNumberIsValid_UpdateSettingAsync()
    {
        await using var dbContext = this._createDbContext();
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.TextTime, Value = "23"
        };

        await cut.Handle(command, default);

        dbContext.ChangeTracker.Clear();

        var guildSettings = await dbContext.GuildLevelSettings
            .FirstAsync(x => x.GuildId == GuildId);

        guildSettings.TextTime.Should().Be(TimeSpan.FromMinutes(23));
    }

    [Fact]
    public async Task WhenUpdatingBase_IfNumberIsValid_UpdateSettingAsync()
    {
        await using var dbContext = this._createDbContext();
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.Base, Value = "23"
        };

        _ = await cut.Handle(command, default);

        dbContext.ChangeTracker.Clear();

        var guildSettings = await dbContext.GuildLevelSettings
            .FirstAsync(x => x.GuildId == GuildId);

        guildSettings.Base.Should().Be(23);
    }

    [Fact]
    public async Task WhenUpdatingModifier_IfNumberIsValid_UpdateSettingAsync()
    {
        await using var dbContext = this._createDbContext();
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.Modifier, Value = "23"
        };

        _ = await cut.Handle(command, default);

        dbContext.ChangeTracker.Clear();

        var guildSettings = await dbContext.GuildLevelSettings
            .FirstAsync(x => x.GuildId == GuildId);

        guildSettings.Modifier.Should().Be(23);
    }

    [Fact]
    public async Task WhenUpdatingAmount_IfNumberIsValid_UpdateSettingAsync()
    {
        await using var dbContext = this._createDbContext();
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.Amount, Value = "23"
        };

        _ = await cut.Handle(command, default);

        dbContext.ChangeTracker.Clear();

        var guildSettings = await dbContext.GuildLevelSettings.FirstAsync(x => x.GuildId == GuildId);

        guildSettings.Amount.Should().Be(23);
    }

    [Fact]
    public async Task WhenUpdatingLogChannel_IfNumberIsValid_UpdateSettingAsync()
    {
        await using var dbContext = this._createDbContext();
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.LogChannel, Value = ChannelId.ToString()
        };

        _ = await cut.Handle(command, default);

        dbContext.ChangeTracker.Clear();

        var guildSettings = await dbContext.GuildLevelSettings
            .FirstAsync(x => x.GuildId == GuildId);

        guildSettings.LevelChannelLogId.Should().Be(ChannelId);
    }

    [Fact]
    public async Task WhenUpdatingLogChannel_IfNumberIs0_UpdateSettingToNullAsync()
    {
        await using var dbContext = this._createDbContext();
        var cut = new SetLevelSettings.Handler(this._mockDbContextFactory);
        var command = new SetLevelSettings.Request
        {
            GuildId = GuildId, LevelSettings = LevelSettingsCommandGroup.LevelSettings.LogChannel, Value = "0"
        };

        var response = await cut.Handle(command, default);

        response.LogChannelId.Should().Be(ChannelId);

        dbContext.ChangeTracker.Clear();

        var guildSettings = await dbContext.GuildLevelSettings
            .FirstAsync(x => x.GuildId == GuildId);

        guildSettings.LevelChannelLogId.Should().BeNull();
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Exceptions;
using Grimoire.Features.Leveling.Commands;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Commands;


[Collection("Test collection")]
public sealed class SetLevelingSettingsCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong CHANNEL_ID = 1;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GUILD_ID,
            LevelSettings = new GuildLevelSettings
            {
                GuildId = GUILD_ID,
            }
        });
        await this._dbContext.AddAsync(new Channel { Id = CHANNEL_ID, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();

        var guild = await this._dbContext.Guilds.FirstAsync(x => x.Id == GUILD_ID);
        guild.ModChannelLog = CHANNEL_ID;

        await this._dbContext.SaveChangesAsync();

    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenUpdatingGuildLevelSettings_IfGuildDoesNotExist_FailResponse()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = 12341234,
            LevelSettings = LevelSettings.TextTime,
            Value = "something"
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));
        response.Should().NotBeNull();
        response?.Message.Should().Be("Could not find guild level settings.");
    }

    [Fact]
    public async Task WhenUpdatingTextTime_IfNumberIsInvalid_FailResponse()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.TextTime,
            Value = "adsfas"
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));
        this._dbContext.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response?.Message.Should().Be("Please give a valid number for TextTime.");
    }

    [Fact]
    public async Task WhenUpdatingBase_IfNumberIsInvalid_FailResponse()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.Base,
            Value = "adsfas"
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));
        response.Should().NotBeNull();
        response?.Message.Should().Be("Please give a valid number for base XP.");
    }

    [Fact]
    public async Task WhenUpdatingModifier_IfNumberIsInvalid_FailResponse()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.Modifier,
            Value = "adsfas"
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));
        response.Should().NotBeNull();
        response?.Message.Should().Be("Please give a valid number for Modifier.");
    }

    [Fact]
    public async Task WhenUpdatingAmount_IfNumberIsInvalid_FailResponse()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.Amount,
            Value = "adsfas"
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));
        response.Should().NotBeNull();
        response?.Message.Should().Be("Please give a valid number for Amount.");
    }

    [Fact]
    public async Task WhenUpdatingLogChannel_IfNumberIsInvalid_FailResponse()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.LogChannel,
            Value = "Something"
        };
        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("Please give a valid channel for Log Channel.");
    }

    [Fact]
    public async Task WhenUpdatingTextTime_IfNumberIsValid_UpdateSettingAsync()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.TextTime,
            Value = "23"
        };

        var response = await CUT.Handle(command, default);

        this._dbContext.ChangeTracker.Clear();

        var guildSettings = await this._dbContext.GuildLevelSettings
            .FirstAsync(x => x.GuildId == GUILD_ID);

        guildSettings.TextTime.Should().Be(TimeSpan.FromMinutes(23));
    }

    [Fact]
    public async Task WhenUpdatingBase_IfNumberIsValid_UpdateSettingAsync()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.Base,
            Value = "23"
        };

        var response = await CUT.Handle(command, default);

        this._dbContext.ChangeTracker.Clear();

        var guildSettings = await this._dbContext.GuildLevelSettings
            .FirstAsync(x => x.GuildId == GUILD_ID);

        guildSettings.Base.Should().Be(23);
    }

    [Fact]
    public async Task WhenUpdatingModifier_IfNumberIsValid_UpdateSettingAsync()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.Modifier,
            Value = "23"
        };

        var response = await CUT.Handle(command, default);

        this._dbContext.ChangeTracker.Clear();

        var guildSettings = await this._dbContext.GuildLevelSettings
            .FirstAsync(x => x.GuildId == GUILD_ID);

        guildSettings.Modifier.Should().Be(23);
    }

    [Fact]
    public async Task WhenUpdatingAmount_IfNumberIsValid_UpdateSettingAsync()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.Amount,
            Value = "23"
        };

        var response = await CUT.Handle(command, default);

        this._dbContext.ChangeTracker.Clear();

        var guildSettings = await this._dbContext.GuildLevelSettings.FirstAsync(x => x.GuildId == GUILD_ID);

        guildSettings.Amount.Should().Be(23);
    }

    [Fact]
    public async Task WhenUpdatingLogChannel_IfNumberIsValid_UpdateSettingAsync()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.LogChannel,
            Value = CHANNEL_ID.ToString()
        };

        var response = await CUT.Handle(command, default);

        this._dbContext.ChangeTracker.Clear();

        var guildSettings = await this._dbContext.GuildLevelSettings
            .FirstAsync(x => x.GuildId == GUILD_ID);

        guildSettings.LevelChannelLogId.Should().Be(CHANNEL_ID);
    }

    [Fact]
    public async Task WhenUpdatingLogChannel_IfNumberIs0_UpdateSettingToNullAsync()
    {
        var CUT = new SetLevelSettings.Handler(this._dbContext);
        var command = new SetLevelSettings.Command
        {
            GuildId = GUILD_ID,
            LevelSettings = LevelSettings.LogChannel,
            Value = "0"
        };

        var response = await CUT.Handle(command, default);

        response.LogChannelId.Should().Be(CHANNEL_ID);

        this._dbContext.ChangeTracker.Clear();

        var guildSettings = await this._dbContext.GuildLevelSettings
            .FirstAsync(x => x.GuildId == GUILD_ID);

        guildSettings.LevelChannelLogId.Should().BeNull();
    }
}

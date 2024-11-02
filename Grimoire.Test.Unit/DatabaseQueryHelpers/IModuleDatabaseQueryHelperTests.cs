// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using EntityFramework.Exceptions.PostgreSQL;
using FluentAssertions;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Domain;
using Grimoire.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class ModuleDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new GrimoireDbContext(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .UseExceptionProcessor()
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = 1,
            LevelSettings = new GuildLevelSettings(),
            UserLogSettings = new GuildUserLogSettings(),
            MessageLogSettings = new GuildMessageLogSettings(),
            ModerationSettings = new GuildModerationSettings()
        });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();


    [Fact]
    public async Task WhenGetModulesOfTypeCalled_ReturnCorrectTypeofModuleAsync()
    {
        var levelingModule = await this._dbContext.Guilds.GetModulesOfType(Module.Leveling)
            .OfType<GuildLevelSettings>()
            .ToListAsync();
        levelingModule.Should().NotBeEmpty();

        var loggingModule = await this._dbContext.Guilds.GetModulesOfType(Module.UserLog)
            .OfType<GuildUserLogSettings>()
            .ToListAsync();
        loggingModule.Should().NotBeEmpty();

        var messageLoggingModule = await this._dbContext.Guilds.GetModulesOfType(Module.MessageLog)
            .OfType<GuildMessageLogSettings>()
            .ToListAsync();
        messageLoggingModule.Should().NotBeEmpty();

        var moderationModule = await this._dbContext.Guilds.GetModulesOfType(Module.Moderation)
            .OfType<GuildModerationSettings>()
            .ToListAsync();
        moderationModule.Should().NotBeEmpty();
    }
}

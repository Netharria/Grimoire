// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Queries;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Queries;

[Collection("Test collection")]
public class GetLevelSettingsQueryTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const bool MODULE_ENABLED = true;
    private readonly TimeSpan _timeSpan = TimeSpan.FromMinutes(3);
    private const int BASE = 15;
    private const int MODIFIER = 100;
    private const int AMOUNT = 5;
    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(
            new Guild
            {
                Id = GUILD_ID,
                LevelSettings = new GuildLevelSettings
                {
                    ModuleEnabled = true,
                    TextTime = _timeSpan,
                    Base = BASE,
                    Modifier = MODIFIER,
                    Amount = AMOUNT,
                }
            });
        await this._dbContext.SaveChangesAsync();
    }
    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task GivenGuildExists_WhenGetLevelSettingsCalled_ReturnLevelSettings()
    {
        var CUT = new GetLevelSettings.Handler(this._dbContext);
        var command = new GetLevelSettings.Query
        {
            GuildId = GUILD_ID,
        };

        var response = await CUT.Handle(command, default);

        response.Should().NotBeNull();
        response.ModuleEnabled.Should().BeTrue();
        response.TextTime.Should().Be(_timeSpan);
        response.Base.Should().Be(BASE);
        response.Modifier.Should().Be(MODIFIER);
        response.Amount.Should().Be(AMOUNT);
        response.LevelChannelLog.Should().BeNull();
    }

    [Fact]
    public async Task GivenGuildDoesNotExists_WhenGetLevelSettingsCalled_ThrowAnticipatedException()
    {
        var CUT = new GetLevelSettings.Handler(this._dbContext);
        var command = new GetLevelSettings.Query
        {
            GuildId = 32165498
        };

        await CUT.Invoking(async x => await x.Handle(command, default))
            .Should().ThrowAsync<AnticipatedException>()
            .WithMessage("Could not find that level settings for that server.");
    }
}

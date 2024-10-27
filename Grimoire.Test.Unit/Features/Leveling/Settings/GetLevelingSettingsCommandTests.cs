// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Features.Leveling.Settings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Settings;

[Collection("Test collection")]
public class GetLevelingSettingsCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong ChannelId = 1;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GuildId,
            LevelSettings = new GuildLevelSettings
            {
                GuildId = GuildId, LevelChannelLogId = ChannelId, ModuleEnabled = true
            }
        });
        await this._dbContext.AddAsync(new Guild { Id = 2, LevelSettings = new GuildLevelSettings { GuildId = 2 } });
        await this._dbContext.AddAsync(new Channel { Id = ChannelId, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task Handle_ReturnsSettingsForCorrectGuild()
    {
        // Arrange
        var request = new GetLevelSettings.Request { GuildId = GuildId };
        var handler = new GetLevelSettings.Handler(this._dbContext);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result!.Should().NotBeNull();
        result!.ModuleEnabled.Should().BeTrue();
        result!.LevelChannelLog.Should().Be(ChannelId);
    }
}

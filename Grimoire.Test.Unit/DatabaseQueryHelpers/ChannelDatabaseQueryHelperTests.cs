// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EntityFramework.Exceptions.PostgreSQL;
using FluentAssertions;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Domain;
using Grimoire.Features.Shared.SharedDtos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class ChannelDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong Channel1 = 1;
    private const ulong Channel2 = 2;
    private const ulong Channel3 = 3;

    private readonly GrimoireDbContext _dbContext = new GrimoireDbContext(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .UseExceptionProcessor()
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GuildId });
        await this._dbContext.AddAsync(new Channel { Id = Channel1, GuildId = GuildId });
        await this._dbContext.AddAsync(new Channel { Id = Channel2, GuildId = GuildId });
        await this._dbContext.AddAsync(new Channel { Id = Channel3, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenChannelsAreNotInDatabase_AddThemAsync()
    {
        //Arrange


        var channelsToAdd = new List<ChannelDto>
        {
            new() { Id = Channel2, GuildId = GuildId },
            new() { Id = Channel3, GuildId = GuildId },
            new() { Id = 4, GuildId = GuildId },
            new() { Id = 5, GuildId = GuildId }
        };

        //Act
        var result = await this._dbContext.Channels.AddMissingChannelsAsync(channelsToAdd);
        await this._dbContext.SaveChangesAsync();

        //Assert
        result.Should().BeTrue();
        this._dbContext.Channels.Should().HaveCount(5);
    }

    [Fact]
    public async Task WhenNoChannelsAreAdded_ReturnsFalse()
    {
        // Arrange
        var channelsToAdd = new List<ChannelDto>(); // No members to add

        // Act
        var result = await this._dbContext.Channels.AddMissingChannelsAsync(channelsToAdd);

        // Assert
        result.Should().BeFalse();
    }
}

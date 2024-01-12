// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.Shared.SharedDtos;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class ChannelDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong CHANNEL_1 = 1;
    private const ulong CHANNEL_2 = 2;
    private const ulong CHANNEL_3 = 3;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.AddAsync(new Channel { Id = CHANNEL_1, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Channel { Id = CHANNEL_2, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Channel { Id = CHANNEL_3, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenChannelsAreNotInDatabase_AddThemAsync()
    {
        //Arrange


        var channelsToAdd = new List<ChannelDto>
        {
            new() { Id = CHANNEL_2, GuildId = GUILD_ID },
            new() { Id = CHANNEL_3, GuildId = GUILD_ID },
            new() { Id = 4, GuildId = GUILD_ID },
            new() { Id = 5, GuildId = GUILD_ID }
        };

        //Act
        var result = await this._dbContext.Channels.AddMissingChannelsAsync(channelsToAdd, default);
        await this._dbContext.SaveChangesAsync();

        //Assert
        result.Should().BeTrue();
        this._dbContext.Channels.Should().HaveCount(5);
    }
}

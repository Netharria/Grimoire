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
using Grimoire.Features.Leveling.Settings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Queries;

[Collection("Test collection")]
public sealed class GetIgnoredItemsQueryTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong ROLE_ID = 1;
    private const ulong CHANNEL_ID = 1;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.AddAsync(new Role
        {
            Id = ROLE_ID,
            GuildId = GUILD_ID,
            IsIgnoredRole = new IgnoredRole { RoleId = ROLE_ID, GuildId = GUILD_ID }
        });
        await this._dbContext.AddAsync(new Channel
        {
            Id = CHANNEL_ID,
            GuildId = GUILD_ID,
            IsIgnoredChannel = new IgnoredChannel { ChannelId = CHANNEL_ID, GuildId = GUILD_ID }
        });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();


    [Fact]
    public async Task WhenCallingGetIgnoredItemsHandler_IfNoIgnoredItems_ReturnFailedResponseAsync()
    {

        this._dbContext.Guilds.Add(new Guild { Id = 34958734 });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetIgnoredItems.Handler(this._dbContext);
        var command = new GetIgnoredItems.Query
        {
            GuildId = 34958734
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("This server does not have any ignored channels, roles or users.");
    }

    [Fact]
    public async Task WhenCallingGetIgnoredItemsHandler_IfIgnoredItems_ReturnSuccessResponseAsync()
    {

        var CUT = new GetIgnoredItems.Handler(this._dbContext);
        var command = new GetIgnoredItems.Query
        {
            GuildId = GUILD_ID
        };

        var response = await CUT.Handle(command, default);

        response.Message.Should().Be($"**Channels**\n<#{CHANNEL_ID}>\n\n**Roles**\n<@&{ROLE_ID}>\n\n**Users**\n");
    }
}

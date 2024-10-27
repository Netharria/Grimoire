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

namespace Grimoire.Test.Unit.Features.Leveling.Settings;

[Collection("Test collection")]
public sealed class GetIgnoredItemsQueryTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong RoleId = 1;
    private const ulong ChannelId = 1;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GuildId });
        await this._dbContext.AddAsync(new Role
        {
            Id = RoleId, GuildId = GuildId, IsIgnoredRole = new IgnoredRole { RoleId = RoleId, GuildId = GuildId }
        });
        await this._dbContext.AddAsync(new Channel
        {
            Id = ChannelId,
            GuildId = GuildId,
            IsIgnoredChannel = new IgnoredChannel { ChannelId = ChannelId, GuildId = GuildId }
        });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();


    [Fact]
    public async Task WhenCallingGetIgnoredItemsHandler_IfNoIgnoredItems_ReturnFailedResponseAsync()
    {
        this._dbContext.Guilds.Add(new Guild { Id = 34958734 });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetIgnoredItems.Handler(this._dbContext);
        var command = new GetIgnoredItems.Query { GuildId = 34958734 };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("This server does not have any ignored channels, roles or users.");
    }

    [Fact]
    public async Task WhenCallingGetIgnoredItemsHandler_IfIgnoredItems_ReturnSuccessResponseAsync()
    {
        var cut = new GetIgnoredItems.Handler(this._dbContext);
        var command = new GetIgnoredItems.Query { GuildId = GuildId };

        var response = await cut.Handle(command, default);

        response.Message.Should().Be($"**Channels**\n<#{ChannelId}>\n\n**Roles**\n<@&{RoleId}>\n\n**Users**\n");
    }
}

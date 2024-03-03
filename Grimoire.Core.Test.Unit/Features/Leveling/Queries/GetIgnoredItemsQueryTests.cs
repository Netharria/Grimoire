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
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Queries;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Queries;

[Collection("Test collection")]
public sealed class GetIgnoredItemsQueryTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong GUILD_ID_2 = 2;
    private const ulong GUILD_ID_3 = 3;
    private const ulong ROLE_ID = 1;
    private const ulong ROLE_ID_2 = 2;
    private const ulong USER_ID = 1;
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
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID_2 });
        await this._dbContext.AddAsync(new Role
        {
            Id = ROLE_ID_2,
            GuildId = GUILD_ID_2,
            IsIgnoredRole = new IgnoredRole { RoleId = ROLE_ID_2, GuildId = GUILD_ID_2 }
        });
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID_3 });
        await this._dbContext.AddAsync(new User
        {
            Id = USER_ID,
            MemberProfiles = new List<Member>
            {
                new()
                {
                    UserId = USER_ID,
                    GuildId = GUILD_ID_3,
                    IsIgnoredMember = new IgnoredMember
                    {
                        UserId = USER_ID,
                        GuildId = GUILD_ID_3,
                    }
                }
            }
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
        await CUT.Invoking(async x => await x.Handle(command, default))
            .Should().ThrowAsync<AnticipatedException>()
            .WithMessage("This server does not have any ignored channels, roles or users.");
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

    [Fact]
    public async Task WhenCallingGetIgnoredItemsHandler_IfIgnoredRole_ReturnSuccessResponseAsync()
    {

        var CUT = new GetIgnoredItems.Handler(this._dbContext);
        var command = new GetIgnoredItems.Query
        {
            GuildId = GUILD_ID_2
        };

        var response = await CUT.Handle(command, default);

        response.Message.Should().Be($"**Channels**\n\n**Roles**\n<@&{ROLE_ID_2}>\n\n**Users**\n");
    }

    [Fact]
    public async Task WhenCallingGetIgnoredItemsHandler_IfIgnoredMember_ReturnSuccessResponseAsync()
    {

        var CUT = new GetIgnoredItems.Handler(this._dbContext);
        var command = new GetIgnoredItems.Query
        {
            GuildId = GUILD_ID_3
        };

        var response = await CUT.Handle(command, default);

        response.Message.Should().Be($"**Channels**\n\n**Roles**\n\n**Users**\n<@!{USER_ID}>\n");
    }
}

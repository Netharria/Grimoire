// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Features.Leveling.Commands;
using Grimoire.Core.Features.Shared.SharedDtos;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Commands;

[Collection("Test collection")]
public class UpdateIgnoreStateForXpGainCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong ROLE_ID = 1;
    private const ulong CHANNEL_ID = 1;
    private const ulong USER_ID = 1;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenAddIgnoreForXpGainCommandHandlerCalled_AddIgnoreStatusAsync()
    {
        var cut = new AddIgnoreForXpGainCommandHandler(this._dbContext);

        var result = await cut.Handle(
            new AddIgnoreForXpGainCommand
            {
                Users = [new UserDto { Id = USER_ID }],
                GuildId = GUILD_ID,
                Channels =
                [
                    new ChannelDto
                    {
                        Id = CHANNEL_ID,
                        GuildId = GUILD_ID
                    }
                ],
                Roles =
                [
                    new RoleDto
                    {
                        Id = ROLE_ID,
                        GuildId = GUILD_ID
                    }
                ]
            }, default);

        result.Message.Should().Be($"<@!{USER_ID}> <@&{ROLE_ID}> <#{CHANNEL_ID}>  are now ignored for xp gain.");

        var member = await this._dbContext.Members.Where(x =>
            x.UserId == USER_ID
            && x.GuildId == GUILD_ID
            ).FirstAsync();

        member.IsIgnoredMember.Should().NotBeNull();

        var role = await this._dbContext.Roles.Where(x =>
            x.Id == ROLE_ID
            && x.GuildId == GUILD_ID
            ).FirstAsync();

        role.IsIgnoredRole.Should().NotBeNull();

        var channel = await this._dbContext.Channels.Where(x =>
            x.Id == CHANNEL_ID
            && x.GuildId == GUILD_ID
            ).FirstAsync();

        channel.IsIgnoredChannel.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenUpdateIgnoreStateForXpGainCommandHandlerCalled_AndThereAreInvalidAndMissingIds_UpdateMessageAsync()
    {
        var cut = new AddIgnoreForXpGainCommandHandler(this._dbContext);

        var result = await cut.Handle(
            new AddIgnoreForXpGainCommand
            {
                GuildId = GUILD_ID,
                InvalidIds = ["asldfkja"]
            }, default);
        this._dbContext.ChangeTracker.Clear();
        result.Message.Should().Be("Could not match asldfkja with a role, channel or user. ");
    }
}

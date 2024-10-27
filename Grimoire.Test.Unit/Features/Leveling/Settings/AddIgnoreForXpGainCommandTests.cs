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
using Grimoire.Domain;
using Grimoire.Features.Leveling.Settings;
using Grimoire.Features.Shared.SharedDtos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Settings;

[Collection("Test collection")]
public sealed class AddIgnoreForXpGainCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong RoleId = 1;
    private const ulong ChannelId = 1;
    private const ulong UserId = 1;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GuildId });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenAddIgnoreForXpGainCommandHandlerCalled_AddIgnoreStatusAsync()
    {
        var cut = new AddIgnoreForXpGain.Handler(this._dbContext);

        var result = await cut.Handle(
            new AddIgnoreForXpGain.Command
            {
                Users = [new UserDto { Id = UserId }],
                GuildId = GuildId,
                Channels =
                [
                    new ChannelDto { Id = ChannelId, GuildId = GuildId }
                ],
                Roles =
                [
                    new RoleDto { Id = RoleId, GuildId = GuildId }
                ]
            }, default);

        result.Message.Should().Be($"<@!{UserId}> <@&{RoleId}> <#{ChannelId}>  are now ignored for xp gain.");

        var member = await this._dbContext.Members.Where(x =>
            x.UserId == UserId
            && x.GuildId == GuildId
        ).FirstAsync();

        member.IsIgnoredMember.Should().NotBeNull();

        var role = await this._dbContext.Roles.Where(x =>
            x.Id == RoleId
            && x.GuildId == GuildId
        ).FirstAsync();

        role.IsIgnoredRole.Should().NotBeNull();

        var channel = await this._dbContext.Channels.Where(x =>
            x.Id == ChannelId
            && x.GuildId == GuildId
        ).FirstAsync();

        channel.IsIgnoredChannel.Should().NotBeNull();
    }

    [Fact]
    public async Task
        WhenUpdateIgnoreStateForXpGainCommandHandlerCalled_AndThereAreInvalidAndMissingIds_UpdateMessageAsync()
    {
        var cut = new AddIgnoreForXpGain.Handler(this._dbContext);

        var result = await cut.Handle(
            new AddIgnoreForXpGain.Command { GuildId = GuildId, InvalidIds = ["asldfkja"] }, default);
        this._dbContext.ChangeTracker.Clear();
        result.Message.Should().Be("Could not match asldfkja with a role, channel or user. ");
    }
}

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
using Grimoire.Exceptions;
using Grimoire.Features.Leveling.Awards;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Awards;

[Collection("Test collection")]
public sealed class ReclaimUserXpCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong USER_ID = 1;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.AddAsync(new User { Id = USER_ID });
        await this._dbContext.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            Xp = 150,
            Type = XpHistoryType.Awarded,
            TimeOut = DateTime.UtcNow.AddMinutes(-5),
        });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            Xp = 150,
            Type = XpHistoryType.Awarded,
            TimeOut = DateTime.UtcNow.AddMinutes(-5),
        });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenReclaimUserXpCommandHandlerCalled_UpdateMembersXpAsync()
    {
        var cut = new ReclaimUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new ReclaimUserXp.Request
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                XpToTake = 200,
                XpOption = XpOption.Amount
            }, default);

        this._dbContext.ChangeTracker.Clear();

        result.Should().NotBeNull();
        result.LogChannelId.Should().BeNull();
        result.XpTaken.Should().Be(200);

        var member = await this._dbContext.Members.Where(x =>
            x.UserId == USER_ID
            && x.GuildId == GUILD_ID
            ).Include(x => x.XpHistory)
            .FirstAsync();

        member.XpHistory.Sum(x => x.Xp).Should().Be(100);
    }

    [Fact]
    public async Task WhenReclaimUserXpCommandHandlerCalled_WithAllArgument_UpdateMembersXpAsync()
    {
        var cut = new ReclaimUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new ReclaimUserXp.Request
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                XpToTake = 0,
                XpOption = XpOption.All
            }, default);
        this._dbContext.ChangeTracker.Clear();

        var member = await  this._dbContext.Members.Where(x =>
            x.UserId == USER_ID
            && x.GuildId == GUILD_ID
            ).FirstAsync();

        member.XpHistory.Sum(x => x.Xp).Should().Be(0);
    }

    [Fact]
    public async Task WhenReclaimUserXpCommandHandlerCalled_WithMissingUser_ReturnFailedResponse()
    {
        var cut = new ReclaimUserXp.Handler(this._dbContext);

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(
            new ReclaimUserXp.Request
            {
                UserId = 20001,
                GuildId = GUILD_ID,
                XpToTake = 20,
                XpOption = XpOption.Amount
            }, default));
        this._dbContext.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response?.Message.Should().Be("<@!20001> was not found. Have they been on the server before?");
    }

    [Fact]
    public async Task WhenReclaimUserXpCommandHandlerCalled_WithXpOptionNotImplemented_ThrowOutOfRangeException()
    {
        var cut = new ReclaimUserXp.Handler(this._dbContext);

        var response = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await cut.Handle(
            new ReclaimUserXp.Request
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                XpToTake = 20,
                XpOption = (XpOption)2
            }, default));
        this._dbContext.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response?.Message.Should().Be("XpOption not implemented in switch statement. (Parameter 'command')");
        response?.ParamName.Should().Be("command");
    }
}

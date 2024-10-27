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
using Grimoire.Features.Leveling.UserCommands;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.UserCommands;

[Collection("Test collection")]
public sealed class GetLeaderboardQueryTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong User1 = 1;
    private const ulong User2 = 2;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GuildId });
        await this._dbContext.AddAsync(new User { Id = User1 });
        await this._dbContext.AddAsync(new Member { UserId = User1, GuildId = GuildId });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = User1,
            GuildId = GuildId,
            Xp = 300,
            Type = XpHistoryType.Awarded,
            TimeOut = DateTime.UtcNow.AddMinutes(-5)
        });

        await this._dbContext.AddAsync(new User { Id = User2 });
        await this._dbContext.AddAsync(new Member { UserId = User2, GuildId = GuildId });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = User2,
            GuildId = GuildId,
            Xp = 0,
            Type = XpHistoryType.Created,
            TimeOut = DateTime.UtcNow.AddMinutes(-5)
        });

        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenCallingGetLeaderboardQueryHandler_IfProvidedUserNotFound_FailResponse()
    {
        var cut = new GetLeaderboard.Handler(this._dbContext);
        var command = new GetLeaderboard.Request { GuildId = GuildId, UserId = 234081234 };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("Could not find user on leaderboard.");
    }

    [Fact]
    public async Task WhenCallingGetLeaderboardQueryHandler_ReturnLeaderboardAsync()
    {
        var cut = new GetLeaderboard.Handler(this._dbContext);
        var command = new GetLeaderboard.Request { GuildId = GuildId };

        var response = await cut.Handle(command, default);

        response.LeaderboardText.Should().Be($"**1** <@!{User1}> **XP:** 300\n**2** <@!{User2}> **XP:** 0\n");
        response.TotalUserCount.Should().Be(2);
    }
}

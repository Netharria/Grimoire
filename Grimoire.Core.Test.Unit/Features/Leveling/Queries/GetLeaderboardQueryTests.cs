// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Queries;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Queries;

[Collection("Test collection")]
public sealed class GetLeaderboardQueryTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong USER_1 = 1;
    private const ulong USER_2 = 2;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.AddAsync(new User { Id = USER_1 });
        await this._dbContext.AddAsync(new Member { UserId = USER_1, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = USER_1,
            GuildId = GUILD_ID,
            Xp = 300,
            Type = XpHistoryType.Awarded,
            TimeOut = DateTime.UtcNow.AddMinutes(-5),
        });

        await this._dbContext.AddAsync(new User { Id = USER_2 });
        await this._dbContext.AddAsync(new Member { UserId = USER_2, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = USER_2,
            GuildId = GUILD_ID,
            Xp = 0,
            Type = XpHistoryType.Created,
            TimeOut = DateTime.UtcNow.AddMinutes(-5),
        });

        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenCallingGetLeaderboardQueryHandler_IfProvidedUserNotFound_FailResponse()
    {

        var CUT = new GetLeaderboardQueryHandler(this._dbContext);
        var command = new GetLeaderboardQuery
        {
            GuildId = GUILD_ID,
            UserId = 234081234
        };

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(command, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("Could not find user on leaderboard.");
    }

    [Fact]
    public async Task WhenCallingGetLeaderboardQueryHandler_ReturnLeaderboardAsync()
    {

        var CUT = new GetLeaderboardQueryHandler(this._dbContext);
        var command = new GetLeaderboardQuery
        {
            GuildId = GUILD_ID
        };

        var response = await CUT.Handle(command, default);

        response.LeaderboardText.Should().Be($"**1** <@!{USER_1}> **XP:** 300\n**2** <@!{USER_2}> **XP:** 0\n");
        response.TotalUserCount.Should().Be(2);
    }
}

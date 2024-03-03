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
    private const ulong GUILD_ID_2 = 2;
    private const ulong USER_1 = 1;
    private const ulong USER_2 = 2;
    private const ulong USER_3 = 3;
    private const ulong USER_4 = 4;
    private const ulong USER_5 = 5;
    private const ulong USER_6 = 6;
    private const ulong USER_7 = 7;
    private const ulong USER_8 = 8;
    private const ulong USER_9 = 9;
    private const ulong USER_10 = 10;
    private const ulong USER_11 = 11;
    private const ulong USER_12 = 12;
    private const ulong USER_13 = 13;
    private const ulong USER_14 = 14;
    private const ulong USER_15 = 15;
    private const ulong USER_16 = 16;
    private const ulong USER_17 = 17;
    private const ulong USER_18 = 18;
    private const ulong USER_19 = 19;
    private const ulong USER_20 = 20;
    private const ulong USER_21 = 21;
    private const ulong USER_22 = 22;
    private const ulong USER_23 = 23;
    private const ulong USER_24 = 24;
    private const ulong USER_25 = 25;
    private const ulong USER_26 = 26;
    private const ulong USER_27 = 27;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID_2 });
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
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = USER_1,
            GuildId = GUILD_ID,
            Xp = 300,
            Type = XpHistoryType.Awarded,
            TimeOut = DateTime.UtcNow.AddMinutes(-5),
        });
        await this.AddUserWithXp(GUILD_ID, USER_2, 20);
        await this.AddUserWithXp(GUILD_ID, USER_3, 30);
        await this.AddUserWithXp(GUILD_ID, USER_4, 40);
        await this.AddUserWithXp(GUILD_ID, USER_5, 50);
        await this.AddUserWithXp(GUILD_ID, USER_6, 60);
        await this.AddUserWithXp(GUILD_ID, USER_7, 70);
        await this.AddUserWithXp(GUILD_ID, USER_8, 80);
        await this.AddUserWithXp(GUILD_ID, USER_9, 90);
        await this.AddUserWithXp(GUILD_ID, USER_10, 100);
        await this.AddUserWithXp(GUILD_ID, USER_11, 110);
        await this.AddUserWithXp(GUILD_ID, USER_12, 120);
        await this.AddUserWithXp(GUILD_ID, USER_13, 130);
        await this.AddUserWithXp(GUILD_ID, USER_14, 140);
        await this.AddUserWithXp(GUILD_ID, USER_15, 150);
        await this.AddUserWithXp(GUILD_ID, USER_16, 160);
        await this.AddUserWithXp(GUILD_ID, USER_17, 170);
        await this.AddUserWithXp(GUILD_ID, USER_18, 180);
        await this.AddUserWithXp(GUILD_ID, USER_19, 190);
        await this.AddUserWithXp(GUILD_ID, USER_20, 200);
        await this.AddUserWithXp(GUILD_ID, USER_21, 210);
        await this.AddUserWithXp(GUILD_ID, USER_22, 220);
        await this.AddUserWithXp(GUILD_ID, USER_23, 230);
        await this.AddUserWithXp(GUILD_ID, USER_24, 240);
        await this.AddUserWithXp(GUILD_ID, USER_25, 250);
        await this.AddUserWithXp(GUILD_ID_2, USER_26, 20);
        await this.AddUserWithXp(GUILD_ID_2, USER_27, 30);
        await this._dbContext.SaveChangesAsync();
    }

    private async Task AddUserWithXp(ulong guildId, ulong userId, long xp)
    {
        await this._dbContext.AddAsync(new User { Id = userId });
        await this._dbContext.AddAsync(new Member { UserId = userId, GuildId = guildId });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = userId,
            GuildId = guildId,
            Xp = xp,
            Type = XpHistoryType.Created,
            TimeOut = DateTime.UtcNow.AddMinutes(-5),
        });
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenCallingGetLeaderboardQueryHandler_IfProvidedUserNotFound_FailResponse()
    {

        var CUT = new GetLeaderboard.Handler(this._dbContext);
        var command = new GetLeaderboard.Query
        {
            GuildId = GUILD_ID,
            UserId = 234081234
        };
        await CUT.Invoking(async x => await x.Handle(command, default))
            .Should().ThrowAsync<AnticipatedException>()
            .WithMessage("Could not find user on leaderboard.");
    }

    [Fact]
    public async Task WhenCallingGetLeaderboardQueryHandler_ReturnLeaderboardAsync()
    {

        var CUT = new GetLeaderboard.Handler(this._dbContext);
        var command = new GetLeaderboard.Query
        {
            GuildId = GUILD_ID
        };

        var response = await CUT.Handle(command, default);

        response.LeaderboardText.Should().Be($"**1** <@!1> **XP:** 600\n**2** <@!25> **XP:** 250\n**3** <@!24> **XP:** 240\n" +
            $"**4** <@!23> **XP:** 230\n**5** <@!22> **XP:** 220\n**6** <@!21> **XP:** 210\n**7** <@!20> **XP:** 200\n" +
            $"**8** <@!19> **XP:** 190\n**9** <@!18> **XP:** 180\n**10** <@!17> **XP:** 170\n**11** <@!16> **XP:** 160\n" +
            $"**12** <@!15> **XP:** 150\n**13** <@!14> **XP:** 140\n**14** <@!13> **XP:** 130\n**15** <@!12> **XP:** 120\n");
        response.TotalUserCount.Should().Be(25);
    }

    [Fact]
    public async Task WhenCallingGetLeaderboardQueryHandlerWithUserNearEnd_ReturnLeaderboardStarting15FromEndAsync()
    {

        var CUT = new GetLeaderboard.Handler(this._dbContext);
        var command = new GetLeaderboard.Query
        {
            GuildId = GUILD_ID,
            UserId = USER_2
        };

        var response = await CUT.Handle(command, default);

        response.LeaderboardText.Should().Be($"**11** <@!16> **XP:** 160\n**12** <@!15> **XP:** 150\n**13** <@!14> **XP:** 140\n" +
            $"**14** <@!13> **XP:** 130\n**15** <@!12> **XP:** 120\n**16** <@!11> **XP:** 110\n**17** <@!10> **XP:** 100\n" +
            $"**18** <@!9> **XP:** 90\n**19** <@!8> **XP:** 80\n**20** <@!7> **XP:** 70\n**21** <@!6> **XP:** 60\n" +
            $"**22** <@!5> **XP:** 50\n**23** <@!4> **XP:** 40\n**24** <@!3> **XP:** 30\n**25** <@!2> **XP:** 20\n");
        response.TotalUserCount.Should().Be(25);
    }

    [Fact]
    public async Task WhenCallingGetLeaderboardQueryHandlerWithUserNearBeginning_ReturnLeaderboardFromStartAsync()
    {

        var CUT = new GetLeaderboard.Handler(this._dbContext);
        var command = new GetLeaderboard.Query
        {
            GuildId = GUILD_ID,
            UserId = USER_25
        };

        var response = await CUT.Handle(command, default);

        response.LeaderboardText.Should().Be($"**1** <@!1> **XP:** 600\n**2** <@!25> **XP:** 250\n**3** <@!24> **XP:** 240\n" +
            $"**4** <@!23> **XP:** 230\n**5** <@!22> **XP:** 220\n**6** <@!21> **XP:** 210\n**7** <@!20> **XP:** 200\n" +
            $"**8** <@!19> **XP:** 190\n**9** <@!18> **XP:** 180\n**10** <@!17> **XP:** 170\n**11** <@!16> **XP:** 160\n" +
            $"**12** <@!15> **XP:** 150\n**13** <@!14> **XP:** 140\n**14** <@!13> **XP:** 130\n**15** <@!12> **XP:** 120\n");
        response.TotalUserCount.Should().Be(25);
    }

    [Fact]
    public async Task WhenCallingGetLeaderboardQueryHandlerWithUserInMiddle_ReturnLeaderboardFrom5BeforeUserAsync()
    {

        var CUT = new GetLeaderboard.Handler(this._dbContext);
        var command = new GetLeaderboard.Query
        {
            GuildId = GUILD_ID,
            UserId = USER_20
        };

        var response = await CUT.Handle(command, default);

        response.LeaderboardText.Should().Be($"**2** <@!25> **XP:** 250\n**3** <@!24> **XP:** 240\n**4** <@!23> **XP:** 230\n" +
            $"**5** <@!22> **XP:** 220\n**6** <@!21> **XP:** 210\n**7** <@!20> **XP:** 200\n**8** <@!19> **XP:** 190\n" +
            $"**9** <@!18> **XP:** 180\n**10** <@!17> **XP:** 170\n**11** <@!16> **XP:** 160\n**12** <@!15> **XP:** 150\n" +
            $"**13** <@!14> **XP:** 140\n**14** <@!13> **XP:** 130\n**15** <@!12> **XP:** 120\n**16** <@!11> **XP:** 110\n");
        response.TotalUserCount.Should().Be(25);
    }

    [Fact]
    public async Task WhenCallingGetLeaderboardQueryHandlerWithShortList_ReturnFullListAsync()
    {

        var CUT = new GetLeaderboard.Handler(this._dbContext);
        var command = new GetLeaderboard.Query
        {
            GuildId = GUILD_ID_2,
            UserId = USER_26
        };

        var response = await CUT.Handle(command, default);

        response.LeaderboardText.Should().Be($"**1** <@!27> **XP:** 30\n**2** <@!26> **XP:** 20\n");
        response.TotalUserCount.Should().Be(2);
    }
}

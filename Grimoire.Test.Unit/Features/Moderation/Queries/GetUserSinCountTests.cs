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
using Grimoire.Features.Moderation.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Moderation.Queries;

[Collection("Test collection")]
public sealed class GetUserSinCountTests(GrimoireCoreFactory factory) : IAsyncLifetime
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
        await this._dbContext.AddAsync(new Guild
        {
            Id = GUILD_ID,
            ModerationSettings = new GuildModerationSettings
            {
                ModuleEnabled = true,
                AutoPardonAfter = TimeSpan.FromDays(60)
            }
        });
        await this._dbContext.AddAsync(new User { Id = USER_ID });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task GivenAUserHasNotBeenOnTheServer_WhenGetUserSinCountIsCalled_ThrowAnticipatedException()
    {
        //Arrange
        var CUT = new GetUserSinCounts.Handler(_dbContext);
        var query = new GetUserSinCounts.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID
        };

        //Act
        var result = await Assert.ThrowsAsync<AnticipatedException>(async () => await CUT.Handle(query, default));

        //Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Could not find that user. Have they been on the server before?");
    }

    [Fact]
    public async Task GivenAGuildDoesNotHaveTheModuleEnabled_WhenGetUserSinCountIsCalled_ReturnNull()
    {
        //Arrange
        await this._dbContext.Guilds.AddAsync(new Guild
        {
            Id = 1234,
            ModerationSettings = new GuildModerationSettings
            {
                ModuleEnabled = false
            }
        });
        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = 1234 });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserSinCounts.Handler(_dbContext);
        var query = new GetUserSinCounts.Query
        {
            UserId = USER_ID,
            GuildId = 1234
        };

        //Act

        var result = await CUT.Handle(query, default);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenAUserDoesNotHaveSins_WhenGetSinCountsIsCalled_ReturnZero()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserSinCounts.Handler(_dbContext);
        var query = new GetUserSinCounts.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID
        };

        //Act

        var result = await CUT.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.WarnCount.Should().Be(0);
        result!.MuteCount.Should().Be(0);
        result!.BanCount.Should().Be(0);
    }

    [Fact]
    public async Task GivenAUserHasWarns_WhenGetSinCountsIsCalled_ReturnCorrectCount()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.Sins.AddRangeAsync(
            new Sin
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                SinType = SinType.Warn,
                ModeratorId = USER_ID,
            },
            new Sin
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                SinType = SinType.Warn,
                ModeratorId = USER_ID,
            },
            new Sin
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                SinType = SinType.Warn,
                ModeratorId = USER_ID,
                SinOn = DateTimeOffset.UtcNow.AddDays(-61)
            });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserSinCounts.Handler(_dbContext);
        var query = new GetUserSinCounts.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID
        };

        //Act

        var result = await CUT.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.WarnCount.Should().Be(2);
        result!.MuteCount.Should().Be(0);
        result!.BanCount.Should().Be(0);
    }

    [Fact]
    public async Task GivenAUserHasMutes_WhenGetSinCountsIsCalled_ReturnCorrectCount()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.Sins.AddRangeAsync(
            new Sin
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                SinType = SinType.Mute,
                ModeratorId = USER_ID,
            },
            new Sin
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                SinType = SinType.Mute,
                ModeratorId = USER_ID,
            },
            new Sin
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                SinType = SinType.Mute,
                ModeratorId = USER_ID,
                SinOn = DateTimeOffset.UtcNow.AddDays(-61)
            });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserSinCounts.Handler(_dbContext);
        var query = new GetUserSinCounts.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID
        };

        //Act

        var result = await CUT.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.WarnCount.Should().Be(0);
        result!.MuteCount.Should().Be(2);
        result!.BanCount.Should().Be(0);
    }

    [Fact]
    public async Task GivenAUserHasBans_WhenGetSinCountsIsCalled_ReturnCorrectCount()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.Sins.AddRangeAsync(
            new Sin
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                SinType = SinType.Ban,
                ModeratorId = USER_ID,
            },
            new Sin
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                SinType = SinType.Ban,
                ModeratorId = USER_ID,
            },
            new Sin
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                SinType = SinType.Ban,
                ModeratorId = USER_ID,
                SinOn = DateTimeOffset.UtcNow.AddDays(-61)
            });
        await this._dbContext.SaveChangesAsync();

        var CUT = new GetUserSinCounts.Handler(_dbContext);
        var query = new GetUserSinCounts.Query
        {
            UserId = USER_ID,
            GuildId = GUILD_ID
        };

        //Act

        var result = await CUT.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.WarnCount.Should().Be(0);
        result!.MuteCount.Should().Be(0);
        result!.BanCount.Should().Be(2);
    }
}

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
    private const ulong GuildId = 1;
    private const ulong UserId = 1;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GuildId,
            ModerationSettings = new GuildModerationSettings
            {
                ModuleEnabled = true, AutoPardonAfter = TimeSpan.FromDays(60)
            }
        });
        await this._dbContext.AddAsync(new User { Id = UserId });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task GivenAUserHasNotBeenOnTheServer_WhenGetUserSinCountIsCalled_ThrowAnticipatedException()
    {
        //Arrange
        var cut = new GetUserSinCounts.Handler(this._dbContext);
        var query = new GetUserSinCounts.Query { UserId = UserId, GuildId = GuildId };

        //Act
        var result = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(query, default));

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
            Id = 1234, ModerationSettings = new GuildModerationSettings { ModuleEnabled = false }
        });
        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = 1234 });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserSinCounts.Handler(this._dbContext);
        var query = new GetUserSinCounts.Query { UserId = UserId, GuildId = 1234 };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenAUserDoesNotHaveSins_WhenGetSinCountsIsCalled_ReturnZero()
    {
        //Arrange

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserSinCounts.Handler(this._dbContext);
        var query = new GetUserSinCounts.Query { UserId = UserId, GuildId = GuildId };

        //Act

        var result = await cut.Handle(query, default);

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

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.Sins.AddRangeAsync(
            new Sin { UserId = UserId, GuildId = GuildId, SinType = SinType.Warn, ModeratorId = UserId },
            new Sin { UserId = UserId, GuildId = GuildId, SinType = SinType.Warn, ModeratorId = UserId },
            new Sin
            {
                UserId = UserId,
                GuildId = GuildId,
                SinType = SinType.Warn,
                ModeratorId = UserId,
                SinOn = DateTimeOffset.UtcNow.AddDays(-61)
            });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserSinCounts.Handler(this._dbContext);
        var query = new GetUserSinCounts.Query { UserId = UserId, GuildId = GuildId };

        //Act

        var result = await cut.Handle(query, default);

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

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.Sins.AddRangeAsync(
            new Sin { UserId = UserId, GuildId = GuildId, SinType = SinType.Mute, ModeratorId = UserId },
            new Sin { UserId = UserId, GuildId = GuildId, SinType = SinType.Mute, ModeratorId = UserId },
            new Sin
            {
                UserId = UserId,
                GuildId = GuildId,
                SinType = SinType.Mute,
                ModeratorId = UserId,
                SinOn = DateTimeOffset.UtcNow.AddDays(-61)
            });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserSinCounts.Handler(this._dbContext);
        var query = new GetUserSinCounts.Query { UserId = UserId, GuildId = GuildId };

        //Act

        var result = await cut.Handle(query, default);

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

        await this._dbContext.Members.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.Sins.AddRangeAsync(
            new Sin { UserId = UserId, GuildId = GuildId, SinType = SinType.Ban, ModeratorId = UserId },
            new Sin { UserId = UserId, GuildId = GuildId, SinType = SinType.Ban, ModeratorId = UserId },
            new Sin
            {
                UserId = UserId,
                GuildId = GuildId,
                SinType = SinType.Ban,
                ModeratorId = UserId,
                SinOn = DateTimeOffset.UtcNow.AddDays(-61)
            });
        await this._dbContext.SaveChangesAsync();

        var cut = new GetUserSinCounts.Handler(this._dbContext);
        var query = new GetUserSinCounts.Query { UserId = UserId, GuildId = GuildId };

        //Act

        var result = await cut.Handle(query, default);

        //Assert
        result.Should().NotBeNull();
        result!.WarnCount.Should().Be(0);
        result!.MuteCount.Should().Be(0);
        result!.BanCount.Should().Be(2);
    }
}

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
using Grimoire.Features.Leveling.Events;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Events;

[Collection("Test collection")]
public sealed class GainUserXpHandlerTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong UserId = 1;
    private const ulong ChannelId = 1;
    private const ulong RoleId1 = 1;
    private const ulong RoleId2 = 2;
    private const ulong RoleId3 = 3;
    private const int GainAmount = 15;

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
            LevelSettings = new GuildLevelSettings
            {
                LevelChannelLogId = ChannelId, ModuleEnabled = true, Amount = GainAmount
            }
        });
        await this._dbContext.AddAsync(new User { Id = UserId });
        await this._dbContext.AddAsync(new Member
        {
            UserId = UserId,
            GuildId = GuildId,
            XpHistory =
            [
                new XpHistory
                {
                    TimeOut = DateTime.UtcNow.AddMinutes(-4),
                    UserId = UserId,
                    GuildId = GuildId,
                    Type = XpHistoryType.Created,
                    Xp = 10
                },
                new XpHistory
                {
                    TimeOut = DateTime.UtcNow.AddMinutes(-5),
                    UserId = UserId,
                    GuildId = GuildId,
                    Type = XpHistoryType.Created,
                    Xp = 10
                }
            ]
        });
        await this._dbContext.AddAsync(new Channel { Id = ChannelId, GuildId = GuildId });
        await this._dbContext.AddAsync(new Role { Id = RoleId1, GuildId = GuildId });
        await this._dbContext.AddAsync(new Role { Id = RoleId2, GuildId = GuildId });
        await this._dbContext.AddAsync(new Role { Id = RoleId3, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_UpdateMemebersXpAsync()
    {
        var cut = new GainUserXp.Handler(this._dbContext);
        var result = await cut.Handle(
            new GainUserXp.Request { UserId = UserId, GuildId = GuildId, ChannelId = ChannelId, RoleIds = [RoleId1] },
            default);

        result.EarnedXp.Should().BeTrue();
        result.PreviousLevel.Should().Be(2);
        result.CurrentLevel.Should().Be(3);
        result.LevelLogChannel.Should().Be(ChannelId);

        this._dbContext.ChangeTracker.Clear();

        var member = await this._dbContext.Members.Where(x =>
            x.UserId == UserId
            && x.GuildId == GuildId
        ).Include(x => x.XpHistory).FirstAsync();

        member.XpHistory.Sum(x => x.Xp).Should().Be(GainAmount + 20);
        var maxHistory = member.XpHistory.MaxBy(x => x.TimeOut);
        maxHistory!.TimeOut.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(3), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_AndMemberIgnored_ReturnFalseResponseAsync()
    {
        await this._dbContext.AddAsync(new Member
        {
            UserId = 10,
            GuildId = GuildId,
            User = new User { Id = 10 },
            IsIgnoredMember = new IgnoredMember { UserId = 10, GuildId = GuildId }
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new GainUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXp.Request { UserId = 10, GuildId = GuildId, ChannelId = ChannelId, RoleIds = [RoleId1] },
            default);
        this._dbContext.ChangeTracker.Clear();
        result.EarnedXp.Should().BeFalse();
    }

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_AndRoleIgnored_ReturnFalseResponseAsync()
    {
        await this._dbContext.AddAsync(new Role
        {
            Id = 10, GuildId = GuildId, IsIgnoredRole = new IgnoredRole { RoleId = 10, GuildId = GuildId }
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new GainUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXp.Request { UserId = UserId, GuildId = GuildId, ChannelId = ChannelId, RoleIds = [10] },
            default);
        this._dbContext.ChangeTracker.Clear();
        result.EarnedXp.Should().BeFalse();
    }

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_AndChannelIgnored_ReturnFalseResponseAsync()
    {
        await this._dbContext.AddAsync(new Channel
        {
            Id = 10, GuildId = GuildId, IsIgnoredChannel = new IgnoredChannel { ChannelId = 10, GuildId = GuildId }
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new GainUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXp.Request { UserId = UserId, GuildId = GuildId, ChannelId = 10, RoleIds = [RoleId1] },
            default);
        this._dbContext.ChangeTracker.Clear();
        result.EarnedXp.Should().BeFalse();
    }

    [Fact]
    public async Task WhenGainuserXpCommandHandlerCalled_AndTimeoutNotExpired_ReturnFalseResponseAsync()
    {
        await this._dbContext.XpHistory.AddAsync(new XpHistory
        {
            TimeOut = DateTime.UtcNow.AddMinutes(5),
            UserId = UserId,
            GuildId = GuildId,
            Type = XpHistoryType.Earned,
            Xp = 0
        });

        await this._dbContext.SaveChangesAsync();

        var cut = new GainUserXp.Handler(this._dbContext);
        var result = await cut.Handle(
            new GainUserXp.Request { UserId = UserId, GuildId = GuildId, ChannelId = ChannelId, RoleIds = [RoleId1] },
            default);

        result.EarnedXp.Should().BeFalse();
    }

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_AndMemberNew_GainXp()
    {
        await this._dbContext.AddAsync(new Member { UserId = 10, GuildId = GuildId, User = new User { Id = 10 } });
        await this._dbContext.SaveChangesAsync();

        var cut = new GainUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXp.Request { UserId = 10, GuildId = GuildId, ChannelId = ChannelId, RoleIds = [RoleId1] },
            default);
        this._dbContext.ChangeTracker.Clear();
        result.EarnedXp.Should().BeTrue();
    }
}

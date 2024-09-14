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
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Commands;

[Collection("Test collection")]
public sealed class GainUserXpCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong USER_ID = 1;
    private const ulong CHANNEL_ID = 1;
    private const ulong ROLE_ID_1 = 1;
    private const int REWARD_LEVEL_1 = 1;
    private const ulong ROLE_ID_2 = 2;
    private const int REWARD_LEVEL_2 = 3;
    private const ulong ROLE_ID_3 = 3;
    private const int REWARD_LEVEL_3 = 5;
    private const int GAIN_AMOUNT = 15;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GUILD_ID,
            LevelSettings = new GuildLevelSettings
            {
                LevelChannelLogId = CHANNEL_ID,
                ModuleEnabled = true,
                Amount = GAIN_AMOUNT
            }
        });
        await this._dbContext.AddAsync(new User { Id = USER_ID });
        await this._dbContext.AddAsync(new Member
        {
            UserId = USER_ID,
            GuildId = GUILD_ID,
            XpHistory =
            [
                new() {
                    TimeOut = DateTime.UtcNow.AddMinutes(-5),
                    UserId = USER_ID,
                    GuildId = GUILD_ID,
                    Type = XpHistoryType.Created,
                    Xp = 10
                },
                new()
                {
                    TimeOut = DateTime.UtcNow.AddMinutes(-5),
                    UserId = USER_ID,
                    GuildId = GUILD_ID,
                    Type = XpHistoryType.Created,
                    Xp = 10
                }
            ]
        });
        await this._dbContext.AddAsync(new Channel { Id = CHANNEL_ID, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID_1, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID_2, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID_3, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Reward { RoleId = ROLE_ID_1, GuildId = GUILD_ID, RewardLevel = REWARD_LEVEL_1, RewardMessage = "Test1" });
        await this._dbContext.AddAsync(new Reward { RoleId = ROLE_ID_2, GuildId = GUILD_ID, RewardLevel = REWARD_LEVEL_2, RewardMessage = "Test2" });
        await this._dbContext.AddAsync(new Reward { RoleId = ROLE_ID_3, GuildId = GUILD_ID, RewardLevel = REWARD_LEVEL_3, RewardMessage = "Test3" });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_UpdateMemebersXpAsync()
    {

        var cut = new GainUserXp.Handler(this._dbContext);
        var result = await cut.Handle(
            new GainUserXp.Command
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                ChannelId = CHANNEL_ID,
                RoleIds = [ ROLE_ID_1 ]
            }, default);

        result.Success.Should().BeTrue();
        result.PreviousLevel.Should().Be(2);
        result.CurrentLevel.Should().Be(3);
        result.LevelLogChannel.Should().Be(CHANNEL_ID);
        result.EarnedRewards.Should().Contain(new GainUserXp.RewardDto[] {
            new() { RoleId = ROLE_ID_1, Message = "Test1" },
            new() { RoleId = ROLE_ID_2, Message = "Test2" }
        });

        this._dbContext.ChangeTracker.Clear();

        var member = await this._dbContext.Members.Where(x =>
            x.UserId == USER_ID
            && x.GuildId == GUILD_ID
            ).Include(x => x.XpHistory).FirstAsync();

        member.XpHistory.Sum(x => x.Xp).Should().Be(GAIN_AMOUNT + 20);
        var maxHistory = member.XpHistory.MaxBy(x => x.TimeOut);
        maxHistory!.TimeOut.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(3), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_AndMemberIgnored_ReturnFalseResponseAsync()
    {
        await this._dbContext.AddAsync(new Member
        {
            UserId = 10,
            GuildId = GUILD_ID,
            User = new User { Id = 10 },
            IsIgnoredMember = new IgnoredMember { UserId = 10, GuildId = GUILD_ID }
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new GainUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXp.Command
            {
                UserId = 10,
                GuildId = GUILD_ID,
                ChannelId = CHANNEL_ID,
                RoleIds = [ ROLE_ID_1 ]
            }, default);
        this._dbContext.ChangeTracker.Clear();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_AndRoleIgnored_ReturnFalseResponseAsync()
    {
        await this._dbContext.AddAsync(new Role
        {
            Id = 10,
            GuildId = GUILD_ID,
            IsIgnoredRole = new IgnoredRole { RoleId = 10, GuildId = GUILD_ID }
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new GainUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXp.Command
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                ChannelId = CHANNEL_ID,
                RoleIds = [ 10 ]
            }, default);
        this._dbContext.ChangeTracker.Clear();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_AndChannelIgnored_ReturnFalseResponseAsync()
    {
        await this._dbContext.AddAsync(new Channel
        {
            Id = 10,
            GuildId = GUILD_ID,
            IsIgnoredChannel = new IgnoredChannel { ChannelId = 10, GuildId = GUILD_ID }
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new GainUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXp.Command
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                ChannelId = 10,
                RoleIds = [ ROLE_ID_1 ]
            }, default);
        this._dbContext.ChangeTracker.Clear();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task WhenGainuserXpCommandHandlerCalled_AndTimeoutNotExpired_ReturnFalseResponseAsync()
    {
        await this._dbContext.XpHistory.AddAsync(new XpHistory
        {

            TimeOut = DateTime.UtcNow.AddMinutes(5),
            UserId = USER_ID,
            GuildId = GUILD_ID,
            Type = XpHistoryType.Earned,
            Xp = 0
        });

        await this._dbContext.SaveChangesAsync();

        var cut = new GainUserXp.Handler(this._dbContext);
        var result = await cut.Handle(
            new GainUserXp.Command
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                ChannelId = CHANNEL_ID,
                RoleIds = [ ROLE_ID_1 ]
            }, default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_AndMemberNew_GainXp()
    {
        await this._dbContext.AddAsync(new Member
        {
            UserId = 10,
            GuildId = GUILD_ID,
            User = new User { Id = 10 }
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new GainUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXp.Command
            {
                UserId = 10,
                GuildId = GUILD_ID,
                ChannelId = CHANNEL_ID,
                RoleIds = [ ROLE_ID_1 ]
            }, default);
        this._dbContext.ChangeTracker.Clear();
        result.Success.Should().BeTrue();
    }
}

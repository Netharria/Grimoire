// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
    private const ulong ROLE_ID = 1;
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
            XpHistory = new List<XpHistory>
            {
                new() {
                    TimeOut = DateTime.UtcNow.AddMinutes(-5),
                    UserId = USER_ID,
                    GuildId = GUILD_ID,
                    Type = XpHistoryType.Created,
                    Xp = 0
                }
            }
        });
        await this._dbContext.AddAsync(new Channel { Id = CHANNEL_ID, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenGainUserXpCommandHandlerCalled_UpdateMemebersXpAsync()
    {

        var cut = new GainUserXpCommandHandler(this._dbContext);
        var result = await cut.Handle(
            new GainUserXpCommand
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                ChannelId = CHANNEL_ID,
                RoleIds = [ ROLE_ID ]
            }, default);

        result.Success.Should().BeTrue();
        result.EarnedRewards.Should().BeEmpty();
        result.PreviousLevel.Should().Be(1);
        result.CurrentLevel.Should().Be(2);
        result.LevelLogChannel.Should().Be(CHANNEL_ID);

        var member = await this._dbContext.Members.Where(x =>
            x.UserId == USER_ID
            && x.GuildId == GUILD_ID
            ).Include(x => x.XpHistory).FirstAsync();

        member.XpHistory.Sum(x => x.Xp).Should().Be(GAIN_AMOUNT);
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

        var cut = new GainUserXpCommandHandler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXpCommand
            {
                UserId = 10,
                GuildId = GUILD_ID,
                ChannelId = CHANNEL_ID,
                RoleIds = [ ROLE_ID ]
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

        var cut = new GainUserXpCommandHandler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXpCommand
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

        var cut = new GainUserXpCommandHandler(this._dbContext);

        var result = await cut.Handle(
            new GainUserXpCommand
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                ChannelId = 10,
                RoleIds = [ ROLE_ID ]
            }, default);
        this._dbContext.ChangeTracker.Clear();
        result.Success.Should().BeFalse();
    }
}

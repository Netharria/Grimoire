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
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Domain;
using Grimoire.Features.Shared.SharedDtos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class MemberDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString + "; Include Error Detail=true")
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const long GUILD_ID = 1;
    private const long GUILD_ID_2 = 3;
    private const long MEMBER_1 = 25;
    private const long MEMBER_2 = 2;
    private const long ROLE_ID = 1;
    private const long CHANNEL_ID = 1;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GUILD_ID,
            LevelSettings = new GuildLevelSettings { ModuleEnabled = true }
        });
        await this._dbContext.AddAsync(new Guild
        {
            Id = GUILD_ID_2,
            LevelSettings = new GuildLevelSettings { ModuleEnabled = true }
        });
        await this._dbContext.AddAsync(new User { Id = MEMBER_1 });
        await this._dbContext.AddAsync(new User { Id = MEMBER_2 });
        await this._dbContext.AddAsync(new Member { UserId = MEMBER_1, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Member { UserId = MEMBER_2, GuildId = GUILD_ID_2 });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = MEMBER_1,
            GuildId = GUILD_ID,
            Xp = 0,
            Type = XpHistoryType.Created,
            TimeOut = DateTime.UtcNow
        });
        await this._dbContext.AddAsync(new NicknameHistory
        {
            UserId = MEMBER_1,
            GuildId = GUILD_ID,
            Nickname = "OldNick",
            Timestamp = DateTime.UtcNow.AddMinutes(-1)
        });
        await this._dbContext.AddAsync(new Avatar
        {
            UserId = MEMBER_1,
            GuildId = GUILD_ID,
            FileName = "OldAvatar",
            Timestamp = DateTime.UtcNow.AddMinutes(-1)
        });
        await this._dbContext.AddAsync(new Role { Id = ROLE_ID, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Channel { Id = CHANNEL_ID, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();
    }
    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenMembersAreNotInDatabase_AddThemAsync()
    {
        var membersToAdd = new List<MemberDto>
        {
            new() { UserId = MEMBER_1, GuildId = GUILD_ID },
            new() { UserId = MEMBER_2, GuildId = GUILD_ID, Nickname = "Nick2", AvatarUrl = "Avatar2" }
        };
        var result = await this._dbContext.Members.AddMissingMembersAsync(membersToAdd, default);

        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();
        result.Should().BeTrue();
        var members = await this._dbContext.Members
            .Where(x => x.GuildId == GUILD_ID)
            .Include(x => x.XpHistory)
            .Include(x => x.NicknamesHistory)
            .Include(x => x.AvatarHistory)
            .ToListAsync();
        members.Should().HaveCount(2)
            .And.AllSatisfy(x =>
            {
                x.UserId.Should().BeOneOf(MEMBER_1, MEMBER_2);
                x.XpHistory.Should().HaveCount(1)
                     .And.AllSatisfy(x =>
                     {
                         x.Type.Should().Be(XpHistoryType.Created);
                         x.Xp.Should().Be(0);
                     });
            });
    }

    [Fact]
    public async Task OnlyAddsNewEntriesWhenUserIdAndGuildIdMatch()
    {
        // Arrange
        var existingMember = new MemberDto { UserId = MEMBER_1, GuildId = GUILD_ID };
        var newMember = new MemberDto { UserId = MEMBER_2, GuildId = GUILD_ID };
        var membersToAdd = new List<MemberDto> { existingMember, newMember };

        // Act
        var result = await this._dbContext.Members.AddMissingMembersAsync(membersToAdd, default);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();
        // Assert
        result.Should().BeTrue();

        var members = await this._dbContext.Members
        .Where(x => x.GuildId == GUILD_ID)
        .ToListAsync();

        members.Should().HaveCount(2)
            .And.ContainSingle(x => x.UserId == MEMBER_1 && x.GuildId == GUILD_ID)
            .And.ContainSingle(x => x.UserId == MEMBER_2 && x.GuildId == GUILD_ID);
    }

    [Fact]
    public async Task WhenNoMembersAreAdded_ReturnsFalse()
    {
        // Arrange
        var membersToAdd = new List<MemberDto>(); // No members to add

        // Act
        var result = await this._dbContext.Members.AddMissingMembersAsync(membersToAdd, default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenNickNamesAreNotInDatabase_AddThemAsync()
    {
        var membersToAdd = new List<MemberDto>
        {
            new() { UserId = MEMBER_1, GuildId = GUILD_ID, Nickname = "Nick" }
        };
        var result = await this._dbContext.NicknameHistory.AddMissingNickNameHistoryAsync(membersToAdd, default);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        var members = await this._dbContext.NicknameHistory
            .Where(x => x.GuildId == GUILD_ID && x.UserId == MEMBER_1)
            .ToListAsync();
        members.Should().HaveCount(2)
            .And.AllSatisfy(x =>
            {
                x.UserId.Should().BeOneOf(MEMBER_1);
                x.Nickname.Should().BeOneOf("OldNick", "Nick");
            });
    }

    [Fact]
    public async Task AddMissingNickNameHistoryAsync_SavesNewNicknameOnlyWhenDifferent()
    {
        // Arrange
        var initialNickname = new NicknameHistory
        {
            UserId = MEMBER_1,
            GuildId = GUILD_ID,
            Nickname = "NewNick"
        };
        await this._dbContext.NicknameHistory.AddAsync(initialNickname);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();
        var membersToAdd = new List<MemberDto>
    {
        new() { UserId = MEMBER_1, GuildId = GUILD_ID, Nickname = "OldNick" } // Same nickname
    };

        // Act
        var result = await this._dbContext.NicknameHistory.AddMissingNickNameHistoryAsync(membersToAdd, default);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();

        // Assert
        result.Should().BeTrue();

        var nicknames = await this._dbContext.NicknameHistory
        .Where(x => x.GuildId == GUILD_ID && x.UserId == MEMBER_1)
        .OrderByDescending(x => x.Timestamp)
        .ToListAsync();

        nicknames.Should().HaveCount(3); // Only one new nickname should be added
        nicknames.First().Nickname.Should().Be("OldNick");
        nicknames.Last().Nickname.Should().Be("OldNick");
    }

    [Fact]
    public async Task WhenNoNicknamesAreAdded_ReturnsFalse()
    {
        // Arrange
        var membersToAdd = new List<MemberDto>(); // No members to add

        // Act
        var result = await this._dbContext.NicknameHistory.AddMissingNickNameHistoryAsync(membersToAdd, default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenAvatarsAreNotInDatabase_AddThemAsync()
    {
        var membersToAdd = new List<MemberDto>
        {
            new() { UserId = MEMBER_1, GuildId = GUILD_ID, AvatarUrl = "Avatar" }
        };
        var result = await this._dbContext.Avatars.AddMissingAvatarsHistoryAsync(membersToAdd, default);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        var members = await this._dbContext.Avatars
            .Where(x => x.GuildId == GUILD_ID && x.UserId == MEMBER_1)
            .ToListAsync();
        members.Should().HaveCount(2)
            .And.AllSatisfy(x =>
            {
                x.UserId.Should().Be(MEMBER_1);
                x.FileName.Should().BeOneOf("OldAvatar", "Avatar");
            });
    }

    [Fact]
    public async Task AddMissingAvatarsHistoryAsync_SavesNewAvatarOnlyWhenDifferent()
    {
        // Arrange
        var initialAvatar = new Avatar
        {
            UserId = MEMBER_1,
            GuildId = GUILD_ID,
            FileName = "NewAvatar"
        };
        await this._dbContext.Avatars.AddAsync(initialAvatar);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();

        var membersToAdd = new List<MemberDto>
        {
            new() { UserId = MEMBER_1, GuildId = GUILD_ID, AvatarUrl = "OldAvatar" }, // OldAvatar
        };

        // Act
        var result = await this._dbContext.Avatars.AddMissingAvatarsHistoryAsync(membersToAdd, default);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();

        // Assert
        result.Should().BeTrue();

        var avatars = await this._dbContext.Avatars
            .Where(x => x.GuildId == GUILD_ID && x.UserId == MEMBER_1)
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync();

        avatars.Should().HaveCount(3);
        avatars.First().FileName.Should().Be("OldAvatar");
        avatars.Last().FileName.Should().Be("OldAvatar");
    }

    [Fact]
    public async Task WhenNoAvatarsAreAdded_ReturnsFalse()
    {
        // Arrange
        var membersToAdd = new List<MemberDto>(); // No members to add

        // Act
        var result = await this._dbContext.Avatars.AddMissingAvatarsHistoryAsync(membersToAdd, default);

        // Assert
        result.Should().BeFalse();
    }





}

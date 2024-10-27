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
    private const long GuildId = 1;
    private const long GuildId2 = 3;
    private const long Member1 = 25;
    private const long Member2 = 2;
    private const long RoleId = 1;
    private const long ChannelId = 1;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString + "; Include Error Detail=true")
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GuildId, LevelSettings = new GuildLevelSettings { ModuleEnabled = true }
        });
        await this._dbContext.AddAsync(new Guild
        {
            Id = GuildId2, LevelSettings = new GuildLevelSettings { ModuleEnabled = true }
        });
        await this._dbContext.AddAsync(new User { Id = Member1 });
        await this._dbContext.AddAsync(new User { Id = Member2 });
        await this._dbContext.AddAsync(new Member { UserId = Member1, GuildId = GuildId });
        await this._dbContext.AddAsync(new Member { UserId = Member2, GuildId = GuildId2 });
        await this._dbContext.AddAsync(new XpHistory
        {
            UserId = Member1,
            GuildId = GuildId,
            Xp = 0,
            Type = XpHistoryType.Created,
            TimeOut = DateTime.UtcNow
        });
        await this._dbContext.AddAsync(new NicknameHistory
        {
            UserId = Member1, GuildId = GuildId, Nickname = "OldNick", Timestamp = DateTime.UtcNow.AddMinutes(-1)
        });
        await this._dbContext.AddAsync(new Avatar
        {
            UserId = Member1, GuildId = GuildId, FileName = "OldAvatar", Timestamp = DateTime.UtcNow.AddMinutes(-1)
        });
        await this._dbContext.AddAsync(new Role { Id = RoleId, GuildId = GuildId });
        await this._dbContext.AddAsync(new Channel { Id = ChannelId, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhereMemberHasId_WhenProvidedValidId_ReturnsResultAsync()
    {
        var result = await this._dbContext.Members.WhereMemberHasId(
            Member2, GuildId2).ToArrayAsync();

        result.Should().HaveCount(1);
        result.Should().AllSatisfy(x => x.UserId.Should().Be(Member2))
            .And.AllSatisfy(x => x.GuildId.Should().Be(GuildId2));
    }

    [Fact]
    public async Task WhenMembersAreNotInDatabase_AddThemAsync()
    {
        var membersToAdd = new List<MemberDto>
        {
            new() { UserId = Member1, GuildId = GuildId },
            new() { UserId = Member2, GuildId = GuildId, Nickname = "Nick2", AvatarUrl = "Avatar2" }
        };
        var result = await this._dbContext.Members.AddMissingMembersAsync(membersToAdd);

        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();
        result.Should().BeTrue();
        var members = await this._dbContext.Members
            .Where(x => x.GuildId == GuildId)
            .Include(x => x.XpHistory)
            .Include(x => x.NicknamesHistory)
            .Include(x => x.AvatarHistory)
            .ToListAsync();
        members.Should().HaveCount(2)
            .And.AllSatisfy(x =>
            {
                x.UserId.Should().BeOneOf(Member1, Member2);
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
        var existingMember = new MemberDto { UserId = Member1, GuildId = GuildId };
        var newMember = new MemberDto { UserId = Member2, GuildId = GuildId };
        var membersToAdd = new List<MemberDto> { existingMember, newMember };

        // Act
        var result = await this._dbContext.Members.AddMissingMembersAsync(membersToAdd);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();
        // Assert
        result.Should().BeTrue();

        var members = await this._dbContext.Members
            .Where(x => x.GuildId == GuildId)
            .ToListAsync();

        members.Should().HaveCount(2)
            .And.ContainSingle(x => x.UserId == Member1 && x.GuildId == GuildId)
            .And.ContainSingle(x => x.UserId == Member2 && x.GuildId == GuildId);
    }

    [Fact]
    public async Task WhenNoMembersAreAdded_ReturnsFalse()
    {
        // Arrange
        var membersToAdd = new List<MemberDto>(); // No members to add

        // Act
        var result = await this._dbContext.Members.AddMissingMembersAsync(membersToAdd);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenNickNamesAreNotInDatabase_AddThemAsync()
    {
        var membersToAdd = new List<MemberDto> { new() { UserId = Member1, GuildId = GuildId, Nickname = "Nick" } };
        var result = await this._dbContext.NicknameHistory.AddMissingNickNameHistoryAsync(membersToAdd);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        var members = await this._dbContext.NicknameHistory
            .Where(x => x.GuildId == GuildId && x.UserId == Member1)
            .ToListAsync();
        members.Should().HaveCount(2)
            .And.AllSatisfy(x =>
            {
                x.UserId.Should().BeOneOf(Member1);
                x.Nickname.Should().BeOneOf("OldNick", "Nick");
            });
    }

    [Fact]
    public async Task AddMissingNickNameHistoryAsync_SavesNewNicknameOnlyWhenDifferent()
    {
        // Arrange
        var initialNickname = new NicknameHistory { UserId = Member1, GuildId = GuildId, Nickname = "NewNick" };
        await this._dbContext.NicknameHistory.AddAsync(initialNickname);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();
        var membersToAdd = new List<MemberDto>
        {
            new() { UserId = Member1, GuildId = GuildId, Nickname = "OldNick" } // Same nickname
        };

        // Act
        var result = await this._dbContext.NicknameHistory.AddMissingNickNameHistoryAsync(membersToAdd);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();

        // Assert
        result.Should().BeTrue();

        var nicknames = await this._dbContext.NicknameHistory
            .Where(x => x.GuildId == GuildId && x.UserId == Member1)
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
        var result = await this._dbContext.NicknameHistory.AddMissingNickNameHistoryAsync(membersToAdd);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenAvatarsAreNotInDatabase_AddThemAsync()
    {
        var membersToAdd = new List<MemberDto> { new() { UserId = Member1, GuildId = GuildId, AvatarUrl = "Avatar" } };
        var result = await this._dbContext.Avatars.AddMissingAvatarsHistoryAsync(membersToAdd);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        var members = await this._dbContext.Avatars
            .Where(x => x.GuildId == GuildId && x.UserId == Member1)
            .ToListAsync();
        members.Should().HaveCount(2)
            .And.AllSatisfy(x =>
            {
                x.UserId.Should().Be(Member1);
                x.FileName.Should().BeOneOf("OldAvatar", "Avatar");
            });
    }

    [Fact]
    public async Task AddMissingAvatarsHistoryAsync_SavesNewAvatarOnlyWhenDifferent()
    {
        // Arrange
        var initialAvatar = new Avatar { UserId = Member1, GuildId = GuildId, FileName = "NewAvatar" };
        await this._dbContext.Avatars.AddAsync(initialAvatar);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();

        var membersToAdd = new List<MemberDto>
        {
            new() { UserId = Member1, GuildId = GuildId, AvatarUrl = "OldAvatar" } // OldAvatar
        };

        // Act
        var result = await this._dbContext.Avatars.AddMissingAvatarsHistoryAsync(membersToAdd);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();

        // Assert
        result.Should().BeTrue();

        var avatars = await this._dbContext.Avatars
            .Where(x => x.GuildId == GuildId && x.UserId == Member1)
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
        var result = await this._dbContext.Avatars.AddMissingAvatarsHistoryAsync(membersToAdd);

        // Assert
        result.Should().BeFalse();
    }
}

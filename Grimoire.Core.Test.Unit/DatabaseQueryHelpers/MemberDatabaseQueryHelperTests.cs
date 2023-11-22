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
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.Shared.SharedDtos;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public class MemberDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const long GUILD_ID = 1;
    private const long MEMBER_1 = 1;
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
        await this._dbContext.AddAsync(new User { Id = MEMBER_1 });
        await this._dbContext.AddAsync(new User { Id = MEMBER_2 });
        await this._dbContext.AddAsync(new Member { UserId = MEMBER_1, GuildId = GUILD_ID });
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
            new() { UserId = MEMBER_2, GuildId = GUILD_ID }
        };
        var result = await this._dbContext.Members.AddMissingMembersAsync(membersToAdd, default);

        await this._dbContext.SaveChangesAsync();
        result.Should().BeTrue();
        this._dbContext.Members.Where(x => x.GuildId == GUILD_ID).Should().HaveCount(2);
    }

    [Fact]
    public async Task WhenWhereLevelingEnabledCalled_GetMembersInGuildsWhereLevelingIsEnabledAsync()
    {

        var result = await this._dbContext.Members.WhereLevelingEnabled().ToArrayAsync();

        result.Should().AllSatisfy(x => x.Guild?.LevelSettings?.ModuleEnabled.Should().BeTrue());
    }

    [Fact]
    public async Task WhenWhereMemberNotIgnoredCalled_GetMembersThatArentIgnoredAsync()
    {
        var ignoredMember = new Member
        {
            UserId = MEMBER_2,
            GuildId = GUILD_ID,
            IsIgnoredMember = new IgnoredMember { UserId = 10, GuildId = GUILD_ID }
        };
        await this._dbContext.AddAsync(ignoredMember);
        await this._dbContext.SaveChangesAsync();



        var result = await this._dbContext.Members.WhereMemberNotIgnored(
            CHANNEL_ID,
            [ ROLE_ID ]).ToArrayAsync();

        result.Should().AllSatisfy(x => x.IsIgnoredMember.Should().BeNull())
            .And.AllSatisfy(x => x.UserId.Should().NotBe(MEMBER_2));
    }

    [Fact]
    public async Task WhenWhereMemberNotIgnoredCalled_GetMembersWithNoIgnoredRoles()
    {
        var ignoredRole = new Role
        {
            Id = 10,
            GuildId = GUILD_ID,
            IsIgnoredRole = new IgnoredRole { RoleId = 10, GuildId = GUILD_ID }
        };
        await this._dbContext.AddAsync(ignoredRole);
        await this._dbContext.SaveChangesAsync();



        var result = await this._dbContext.Members.WhereMemberNotIgnored(
            CHANNEL_ID,
            [ 10 ]).ToArrayAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenWhereMemberNotIgnoredCalled_GetMembersNotInIgnoredChannel()
    {
        var ignoredChannel = new Channel
        {
            Id = 10,
            GuildId = GUILD_ID,
            IsIgnoredChannel = new IgnoredChannel { ChannelId = 10, GuildId = GUILD_ID }
        };
        await this._dbContext.AddAsync(ignoredChannel);
        await this._dbContext.SaveChangesAsync();



        var result = await this._dbContext.Members.WhereMemberNotIgnored(
            10,
            [ ROLE_ID ]).ToArrayAsync();

        result.Should().BeEmpty();
    }
}

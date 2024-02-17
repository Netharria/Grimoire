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
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers;

[Collection("Test collection")]
public sealed class IMemberDatabaseQueryHelperTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const long GUILD_ID = 1;
    private const long MEMBER_1 = 1;
    private const long MEMBER_2 = 2;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.AddAsync(new User { Id = MEMBER_1 });
        await this._dbContext.AddAsync(new User { Id = MEMBER_2 });
        await this._dbContext.AddAsync(new Member { UserId = MEMBER_1, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Member { UserId = MEMBER_2, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();
    }
    public Task DisposeAsync() => this._resetDatabase();


    [Fact]
    public async Task WhereMembersHaveIds_WhenProvidedValidIds_ReturnsResultAsync()
    {

        var result = await this._dbContext.Members.WhereMembersHaveIds(
            [ MEMBER_1 ], GUILD_ID).ToArrayAsync();

        result.Should().HaveCount(1);
        result.Should().AllSatisfy(x => x.UserId.Should().Be(MEMBER_1))
            .And.AllSatisfy(x => x.GuildId.Should().Be(GUILD_ID));
    }

    [Fact]
    public async Task WWhereMemberHasId_WhenProvidedValidId_ReturnsResultAsync()
    {

        var result = await this._dbContext.Members.WhereMemberHasId(
            MEMBER_2, GUILD_ID).ToArrayAsync();

        result.Should().HaveCount(1);
        result.Should().AllSatisfy(x => x.UserId.Should().Be(MEMBER_2))
            .And.AllSatisfy(x => x.GuildId.Should().Be(GUILD_ID));
    }
}

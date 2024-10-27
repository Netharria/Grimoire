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
using Grimoire.Exceptions;
using Grimoire.Features.Leveling.Awards;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Awards;

[Collection("Test collection")]
public sealed class AwardUserXpCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong UserId = 1;
    private const ulong ChannelId = 1;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GuildId });
        await this._dbContext.AddAsync(new Channel { Id = UserId, GuildId = GuildId });
        await this._dbContext.AddAsync(new User { Id = ChannelId });
        await this._dbContext.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.SaveChangesAsync();

        var guild = await this._dbContext.Guilds.FirstAsync(x => x.Id == GuildId);
        guild.ModChannelLog = ChannelId;

        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenAwardUserXpCommandHandlerCalled_UpdateMemebersXpAsync()
    {
        var cut = new AwardUserXp.Handler(this._dbContext);

        var result = await cut.Handle(
            new AwardUserXp.Request { UserId = UserId, GuildId = GuildId, XpToAward = 20 }, default);

        this._dbContext.ChangeTracker.Clear();

        var member = await this._dbContext.Members.Where(x =>
            x.UserId == UserId
            && x.GuildId == GuildId
        ).Include(x => x.XpHistory).FirstAsync();
        member.XpHistory.Sum(x => x.Xp).Should().Be(20);

        result.Should().NotBeNull();
        result.LogChannelId.Should().NotBeNull();
        result.LogChannelId.Should().Be(ChannelId);
    }

    [Fact]
    public async Task WhenAwardUserXpCommandHandlerCalled_WithMissingUser_ReturnFailedResponse()
    {
        var cut = new AwardUserXp.Handler(this._dbContext);
        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(
            new AwardUserXp.Request { UserId = 20001, GuildId = GuildId, XpToAward = 20 }, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("<@!20001> was not found. Have they been on the server before?");
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityFramework.Exceptions.PostgreSQL;
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Exceptions;
using Grimoire.Features.Leveling.Awards;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Awards;

[Collection("Test collection")]
public sealed class ReclaimUserXpCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong UserId = 1;

    private readonly Func<GrimoireDbContext> _createDbContext = () => new GrimoireDbContext(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .UseExceptionProcessor()
            .Options);

    private readonly IDbContextFactory<GrimoireDbContext> _mockDbContextFactory =
        Substitute.For<IDbContextFactory<GrimoireDbContext>>();

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Guild { Id = GuildId });
        await dbContext.AddAsync(new User { Id = UserId });
        await dbContext.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await dbContext.AddAsync(new XpHistory
        {
            UserId = UserId,
            GuildId = GuildId,
            Xp = 150,
            Type = XpHistoryType.Awarded,
            TimeOut = DateTime.UtcNow.AddMinutes(-5)
        });
        await dbContext.AddAsync(new XpHistory
        {
            UserId = UserId,
            GuildId = GuildId,
            Xp = 150,
            Type = XpHistoryType.Awarded,
            TimeOut = DateTime.UtcNow.AddMinutes(-5)
        });
        await dbContext.SaveChangesAsync();

        this._mockDbContextFactory.CreateDbContextAsync().Returns(this._createDbContext());
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenReclaimUserXpCommandHandlerCalled_UpdateMembersXpAsync()
    {
        await using var dbContext = this._createDbContext();
        var cut = new ReclaimUserXp.Handler(this._mockDbContextFactory);

        var result = await cut.Handle(
            new ReclaimUserXp.Request
            {
                UserId = UserId, GuildId = GuildId, XpToTake = 200, XpOption = ReclaimUserXp.XpOption.Amount
            }, CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        result.Should().NotBeNull();
        result.XpTaken.Should().Be(200);

        var member = await dbContext.Members.Where(x =>
                x.UserId == UserId
                && x.GuildId == GuildId
            ).Include(x => x.XpHistory)
            .FirstAsync();

        member.XpHistory.Sum(x => x.Xp).Should().Be(100);
    }

    [Fact]
    public async Task WhenReclaimUserXpCommandHandlerCalled_WithAllArgument_UpdateMembersXpAsync()
    {
        await using var dbContext = this._createDbContext();
        var cut = new ReclaimUserXp.Handler(this._mockDbContextFactory);

        _ = await cut.Handle(
            new ReclaimUserXp.Request { UserId = UserId, GuildId = GuildId, XpToTake = 0, XpOption = ReclaimUserXp.XpOption.All },
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        var member = await dbContext.Members
            .Where(x =>
                x.UserId == UserId
                && x.GuildId == GuildId
            ).Include(member => member.XpHistory)
            .FirstAsync();

        member.XpHistory.Sum(x => x.Xp).Should().Be(0);
    }

    [Fact]
    public async Task WhenReclaimUserXpCommandHandlerCalled_WithMissingUser_ReturnFailedResponse()
    {
        await using var dbContext = this._createDbContext();
        var cut = new ReclaimUserXp.Handler(this._mockDbContextFactory);

        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(
            new ReclaimUserXp.Request { UserId = 20001, GuildId = GuildId, XpToTake = 20, XpOption = ReclaimUserXp.XpOption.Amount },
            CancellationToken.None));
        dbContext.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response.Message.Should().Be("<@!20001> was not found. Have they been on the server before?");
    }

    [Fact]
    public async Task WhenReclaimUserXpCommandHandlerCalled_WithXpOptionNotImplemented_ThrowOutOfRangeException()
    {
        await using var dbContext = this._createDbContext();
        var cut = new ReclaimUserXp.Handler(this._mockDbContextFactory);

        var response = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await cut.Handle(
            new ReclaimUserXp.Request { UserId = UserId, GuildId = GuildId, XpToTake = 20, XpOption = (ReclaimUserXp.XpOption)2 },
            CancellationToken.None));
        dbContext.ChangeTracker.Clear();
        response.Should().NotBeNull();
        response.Message.Should().Be("XpOption not implemented in switch statement. (Parameter 'command')");
        response.ParamName.Should().Be("command");
    }
}

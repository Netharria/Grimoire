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
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Features.Logging.Settings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Grimoire.Test.Unit.Features.Logging.MessageLogging.Commands;

[Collection("Test collection")]
public sealed class GetMessageLogOverrideTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong Channel1Id = 1;
    private const ulong Channel2Id = 2;

    private readonly Func<GrimoireDbContext> _createDbContext = () => new GrimoireDbContext(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly IDbContextFactory<GrimoireDbContext> _mockDbContextFactory =
        Substitute.For<IDbContextFactory<GrimoireDbContext>>();

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Guild { Id = GuildId });
        await dbContext.AddAsync(new Channel { Id = Channel1Id, GuildId = GuildId });
        await dbContext.AddAsync(new Channel { Id = Channel2Id, GuildId = GuildId });
        await dbContext.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = Channel1Id, GuildId = GuildId, ChannelOption = MessageLogOverrideOption.AlwaysLog
        });
        await dbContext.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = Channel2Id, GuildId = GuildId, ChannelOption = MessageLogOverrideOption.NeverLog
        });
        await dbContext.SaveChangesAsync();

        this._mockDbContextFactory.CreateDbContextAsync().Returns(this._createDbContext());
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenGetMessageLogOverrideCalled_ReturnOverrides()
    {
        await using var dbContext = this._createDbContext();
        var cut = new GetMessageLogOverrides.Handler(this._mockDbContextFactory);

        var result = await cut.Handle(
            new GetMessageLogOverrides.Query { GuildId = GuildId }, CancellationToken.None).ToListAsync();

        dbContext.ChangeTracker.Clear();

        result.Should()
            .NotBeNullOrEmpty().And
            .HaveCount(2).And
            .ContainEquivalentOf(new GetMessageLogOverrides.Response
            {
                ChannelId = Channel1Id, ChannelOption = MessageLogOverrideOption.AlwaysLog
            }).And
            .ContainEquivalentOf(new GetMessageLogOverrides.Response
            {
                ChannelId = Channel2Id, ChannelOption = MessageLogOverrideOption.NeverLog
            });
    }

    [Fact]
    public async Task WhenGuildDoesntExist_WhenGetMessageLogOverrideCalled_ReturnEmptyList()
    {
        await using var dbContext = this._createDbContext();
        var cut = new GetMessageLogOverrides.Handler(this._mockDbContextFactory);

        var result = await cut.Handle(
            new GetMessageLogOverrides.Query { GuildId = 1321654 }, CancellationToken.None).ToListAsync();

        dbContext.ChangeTracker.Clear();

        result.Should().NotBeNull().And
            .BeEmpty();
    }
}

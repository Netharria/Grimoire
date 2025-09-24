// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Domain.Obsolete;
using Grimoire.Features.Logging.MessageLogging.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Grimoire.Test.Unit.Features.Logging.MessageLogging.Events;

[Collection("Test collection")]
public class LinkProxyMessageTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong ChannelId = 1;
    private const ulong UserId = 1;
    private const ulong MessageId1 = 1;
    private const ulong MessageId2 = 2;

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
        await dbContext.AddAsync(new Guild
        {
            Id = GuildId,
            MessageLogSettings = new GuildMessageLogSettings { GuildId = GuildId, ModuleEnabled = true }
        });
        await dbContext.AddAsync(new Channel { Id = ChannelId, GuildId = GuildId });
        await dbContext.AddAsync(new User { Id = UserId });
        await dbContext.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await dbContext.AddAsync(new Message
        {
            Id = MessageId1, UserId = UserId, GuildId = GuildId, ChannelId = ChannelId
        });
        await dbContext.AddAsync(new Message
        {
            Id = MessageId2, UserId = UserId, GuildId = GuildId, ChannelId = ChannelId
        });
        await dbContext.SaveChangesAsync();

        this._mockDbContextFactory.CreateDbContextAsync().Returns(this._createDbContext());
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenProxyMessageIsSent_LinkWithOriginalMessage()
    {
        await using var dbContext = this._createDbContext();
        var logger = new NullLogger<LinkProxyMessage.Handler>();
        var cut = new LinkProxyMessage.Handler(this._mockDbContextFactory, logger);

        await cut.Handle(
            new LinkProxyMessage.Command
            {
                OriginalMessageId = MessageId1,
                ProxyMessageId = MessageId2,
                GuildId = GuildId,
                SystemId = "SystemId",
                MemberId = "MemberId"
            }, CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        var result = await dbContext.ProxiedMessages
            .FirstOrDefaultAsync(x =>
                x.OriginalMessageId == MessageId1
                && x.ProxyMessageId == MessageId2);

        result.Should().NotBeNull();
        result!.SystemId.Should().Be("SystemId");
        result.MemberId.Should().Be("MemberId");
    }
}

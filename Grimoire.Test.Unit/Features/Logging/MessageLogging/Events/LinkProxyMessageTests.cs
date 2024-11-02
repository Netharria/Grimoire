// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Features.Logging.MessageLogging.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Grimoire.Test.Unit.Features.MessageLogging.Commands;

[Collection("Test collection")]
public class LinkProxyMessageTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong ChannelId = 1;
    private const ulong UserId = 1;
    private const ulong MessageId1 = 1;
    private const ulong MessageId2 = 2;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GuildId,
            MessageLogSettings = new GuildMessageLogSettings { GuildId = GuildId, ModuleEnabled = true }
        });
        await this._dbContext.AddAsync(new Channel { Id = ChannelId, GuildId = GuildId });
        await this._dbContext.AddAsync(new User { Id = UserId });
        await this._dbContext.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await this._dbContext.AddAsync(new Message
        {
            Id = MessageId1, UserId = UserId, GuildId = GuildId, ChannelId = ChannelId
        });
        await this._dbContext.AddAsync(new Message
        {
            Id = MessageId2, UserId = UserId, GuildId = GuildId, ChannelId = ChannelId
        });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenProxyMessageIsSent_LinkWithOriginalMessage()
    {
        var logger = new NullLogger<LinkProxyMessage.Handler>();
        var cut = new LinkProxyMessage.Handler(this._dbContext, logger);

        await cut.Handle(
            new LinkProxyMessage.Command
            {
                OriginalMessageId = MessageId1,
                ProxyMessageId = MessageId2,
                GuildId = GuildId,
                SystemId = "SystemId",
                MemberId = "MemberId"
            }, default);

        this._dbContext.ChangeTracker.Clear();

        var result = await this._dbContext.ProxiedMessages
            .FirstOrDefaultAsync(x =>
                x.OriginalMessageId == MessageId1
                && x.ProxyMessageId == MessageId2);

        result.Should().NotBeNull();
        result!.SystemId.Should().Be("SystemId");
        result!.MemberId.Should().Be("MemberId");
    }
}

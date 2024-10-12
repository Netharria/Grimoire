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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Grimoire.Test.Unit.Features.MessageLogging.Commands;

[Collection("Test collection")]
public class LinkProxyMessageTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong CHANNEL_ID = 1;
    private const ulong USER_ID = 1;
    private const ulong MESSAGE_ID_1 = 1;
    private const ulong MESSAGE_ID_2 = 2;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild
        {
            Id = GUILD_ID,
            MessageLogSettings = new GuildMessageLogSettings
            {
                GuildId = GUILD_ID,
                ModuleEnabled = true
            }
        });
        await this._dbContext.AddAsync(new Channel { Id = CHANNEL_ID, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new User { Id = USER_ID });
        await this._dbContext.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.AddAsync(new Message { Id = MESSAGE_ID_1, UserId = USER_ID, GuildId = GUILD_ID, ChannelId = CHANNEL_ID });
        await this._dbContext.AddAsync(new Message { Id = MESSAGE_ID_2, UserId = USER_ID, GuildId = GUILD_ID, ChannelId = CHANNEL_ID });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenProxyMessageIsSent_LinkWithOriginalMessage()
    {
        var logger = new NullLogger<Grimoire.Features.MessageLogging.Commands.LinkProxyMessage.Handler>();
        var cut = new Grimoire.Features.MessageLogging.Commands.LinkProxyMessage.Handler(this._dbContext, logger);

        await cut.Handle(new Grimoire.Features.MessageLogging.Commands.LinkProxyMessage.Command
        {
            OriginalMessageId = MESSAGE_ID_1,
            ProxyMessageId = MESSAGE_ID_2,
            GuildId = GUILD_ID,
            SystemId = "SystemId",
            MemberId = "MemberId"
        }, default);

        this._dbContext.ChangeTracker.Clear();

        var result = await this._dbContext.ProxiedMessages
            .FirstOrDefaultAsync(x =>
            x.OriginalMessageId == MESSAGE_ID_1
            && x.ProxyMessageId == MESSAGE_ID_2);

        result.Should().NotBeNull();
        result!.SystemId.Should().Be("SystemId");
        result!.MemberId.Should().Be("MemberId");
    }
}

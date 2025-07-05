// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityFramework.Exceptions.PostgreSQL;
using FluentAssertions;
using Grimoire.Domain;
using Grimoire.Features.Logging.MessageLogging.Events;
using Grimoire.Features.Shared.SharedDtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Grimoire.Test.Unit.Features.Logging.MessageLogging.Events;

[Collection("Test collection")]
public class AddMessageEventTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong ChannelId = 1;
    private const ulong UserId = 1;
    private const ulong UserId2 = 2;
    private const ulong MessageId1 = 1;

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
        await dbContext.AddAsync(new Guild
        {
            Id = GuildId,
            MessageLogSettings = new GuildMessageLogSettings { GuildId = GuildId, ModuleEnabled = true }
        });
        await dbContext.AddAsync(new Channel { Id = ChannelId, GuildId = GuildId });
        await dbContext.AddAsync(new User { Id = UserId });
        await dbContext.AddAsync(new Member { UserId = UserId, GuildId = GuildId });
        await dbContext.AddAsync(new User { Id = UserId2 });
        await dbContext.AddAsync(new Member { UserId = UserId2, GuildId = GuildId });
        await dbContext.SaveChangesAsync();

        this._mockDbContextFactory.CreateDbContextAsync().Returns(this._createDbContext());
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenGuildNotFound_ThrowsException()
    {
        var logger = new NullLogger<AddMessageEvent.Handler>();
        var cut = new AddMessageEvent.Handler(this._mockDbContextFactory, logger);

        var act = async () => await cut.Handle(
            new AddMessageEvent.Command
            {
                GuildId = 999, // Non-existent GuildId
                ChannelId = ChannelId,
                UserId = UserId,
                MessageId = MessageId1,
                MessageContent = string.Empty,
                Attachments = [],
                ParentChannelTree = []
            }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Guild was not found in database.");
    }

    [Fact]
    public async Task WhenModuleDisabled_MessageIsNotSaved()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Guild
        {
            Id = 45, MessageLogSettings = new GuildMessageLogSettings { GuildId = 45, ModuleEnabled = false }
        });
        await dbContext.AddAsync(new Channel { Id = 46, GuildId = 45 });
        await dbContext.AddAsync(new Member { UserId = UserId, GuildId = 45 });
        await dbContext.SaveChangesAsync();

        var logger = new NullLogger<AddMessageEvent.Handler>();
        var cut = new AddMessageEvent.Handler(this._mockDbContextFactory, logger);

        await cut.Handle(
            new AddMessageEvent.Command
            {
                GuildId = 45,
                ChannelId = 46,
                UserId = UserId,
                MessageId = MessageId1,
                MessageContent = string.Empty,
                Attachments = [],
                ParentChannelTree = []
            }, default);

        var message = await dbContext.Messages.FindAsync(MessageId1);
        message.Should().BeNull();
    }

    [Fact]
    public async Task WhenChannelDoesNotExist_ChannelIsAdded()
    {
        await using var dbContext = this._createDbContext();

        var logger = new NullLogger<AddMessageEvent.Handler>();
        var cut = new AddMessageEvent.Handler(this._mockDbContextFactory, logger);

        await cut.Handle(
            new AddMessageEvent.Command
            {
                GuildId = GuildId,
                ChannelId = 999, // Non-existent ChannelId
                UserId = UserId,
                MessageId = MessageId1,
                MessageContent = string.Empty,
                Attachments = [],
                ParentChannelTree = []
            }, default);

        var channel = await dbContext.Channels.FindAsync(999UL);
        channel.Should().NotBeNull();
        channel!.Id.Should().Be(999);
        channel.GuildId.Should().Be(GuildId);
    }

    [Fact]
    public async Task WhenMemberDoesNotExist_MemberIsAdded()
    {
        await using var dbContext = this._createDbContext();

        var logger = new NullLogger<AddMessageEvent.Handler>();
        var cut = new AddMessageEvent.Handler(this._mockDbContextFactory, logger);

        await cut.Handle(
            new AddMessageEvent.Command
            {
                GuildId = GuildId,
                ChannelId = ChannelId,
                UserId = 999, // Non-existent UserId
                MessageId = MessageId1,
                MessageContent = string.Empty,
                Attachments = [],
                ParentChannelTree = []
            }, default);

        var member = await dbContext.Members
            .Include(member => member.XpHistory)
            .FirstOrDefaultAsync(member => member.UserId == 999UL && member.GuildId == GuildId);
        member.Should().NotBeNull();
        member!.UserId.Should().Be(999);
        member.GuildId.Should().Be(GuildId);
        member.XpHistory.First().Type.Should().Be(XpHistoryType.Created);
    }

    [Fact]
    public async Task WhenParentChannelNeverLogAndChildChannelAlwaysLog_MessageIsAdded()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Channel { Id = 999, GuildId = GuildId });
        await dbContext.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = ChannelId, ChannelOption = MessageLogOverrideOption.NeverLog, GuildId = GuildId
        });
        await dbContext.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = 999, // Child Channel
            ChannelOption = MessageLogOverrideOption.AlwaysLog,
            GuildId = GuildId
        });
        await dbContext.SaveChangesAsync();

        var logger = new NullLogger<AddMessageEvent.Handler>();
        var cut = new AddMessageEvent.Handler(this._mockDbContextFactory, logger);

        await cut.Handle(
            new AddMessageEvent.Command
            {
                GuildId = GuildId,
                ChannelId = 999, // Child Channel
                UserId = UserId,
                MessageId = MessageId1,
                MessageContent = "Test message",
                Attachments = [],
                ParentChannelTree = [999UL, ChannelId] // Parent and Child Channel
            }, default);

        var message = await dbContext.Messages
            .Include(x => x.MessageHistory)
            .FirstOrDefaultAsync(x => x.Id == MessageId1);
        message.Should().NotBeNull();
        message!.Id.Should().Be(MessageId1);
        message.ChannelId.Should().Be(999);
        message.GuildId.Should().Be(GuildId);
        message.MessageHistory.First().MessageContent.Should().Be("Test message");
    }

    [Fact]
    public async Task WhenParentChannelAlwaysLogAndChildChannelNeverLog_MessageIsNotAdded()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Channel { Id = 999, GuildId = GuildId });
        await dbContext.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = ChannelId, ChannelOption = MessageLogOverrideOption.AlwaysLog, GuildId = GuildId
        });
        await dbContext.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = 999, // Child Channel
            ChannelOption = MessageLogOverrideOption.NeverLog,
            GuildId = GuildId
        });
        await dbContext.SaveChangesAsync();

        var logger = new NullLogger<AddMessageEvent.Handler>();
        var cut = new AddMessageEvent.Handler(this._mockDbContextFactory, logger);

        await cut.Handle(
            new AddMessageEvent.Command
            {
                GuildId = GuildId,
                ChannelId = 999, // Child Channel
                UserId = UserId,
                MessageId = MessageId1,
                MessageContent = "Test message",
                Attachments = [],
                ParentChannelTree = [999UL, ChannelId] // Parent and Child Channel
            }, default);

        var message = await dbContext.Messages
            .Include(x => x.MessageHistory)
            .FirstOrDefaultAsync(x => x.Id == MessageId1);
        message.Should().BeNull();
    }

    [Fact]
    public async Task WhenParentChannelAlwaysLogAndChildChannelDefault_MessageIsAdded()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Channel { Id = 999, GuildId = GuildId });
        await dbContext.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = ChannelId, ChannelOption = MessageLogOverrideOption.AlwaysLog, GuildId = GuildId
        });
        await dbContext.SaveChangesAsync();

        var logger = new NullLogger<AddMessageEvent.Handler>();
        var cut = new AddMessageEvent.Handler(this._mockDbContextFactory, logger);

        await cut.Handle(
            new AddMessageEvent.Command
            {
                GuildId = GuildId,
                ChannelId = 999, // Child Channel
                UserId = UserId,
                MessageId = MessageId1,
                MessageContent = "Test message",
                Attachments = [],
                ParentChannelTree = [999UL, ChannelId] // Parent and Child Channel
            }, default);

        var message = await dbContext.Messages
            .Include(x => x.MessageHistory)
            .FirstOrDefaultAsync(x => x.Id == MessageId1);
        message.Should().NotBeNull();
        message!.Id.Should().Be(MessageId1);
        message.ChannelId.Should().Be(999);
        message.GuildId.Should().Be(GuildId);
        message.MessageHistory.First().MessageContent.Should().Be("Test message");
    }

    [Fact]
    public async Task WhenParentChannelNeverLogAndChildChannelDefault_MessageIsNotAdded()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Channel { Id = 999, GuildId = GuildId });
        await dbContext.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = ChannelId, ChannelOption = MessageLogOverrideOption.NeverLog, GuildId = GuildId
        });
        await dbContext.SaveChangesAsync();

        var logger = new NullLogger<AddMessageEvent.Handler>();
        var cut = new AddMessageEvent.Handler(this._mockDbContextFactory, logger);

        await cut.Handle(
            new AddMessageEvent.Command
            {
                GuildId = GuildId,
                ChannelId = 999, // Child Channel
                UserId = UserId,
                MessageId = MessageId1,
                MessageContent = "Test message",
                Attachments = [],
                ParentChannelTree = [999UL, ChannelId] // Parent and Child Channel
            }, default);

        var message = await dbContext.Messages
            .Include(x => x.MessageHistory)
            .FirstOrDefaultAsync(x => x.Id == MessageId1);
        message.Should().BeNull();
    }

    [Fact]
    public async Task WhenDuplicateMessageAdded_DbUpdateExceptionThrown()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.AddAsync(new Message
        {
            Id = MessageId1, ChannelId = ChannelId, GuildId = GuildId, UserId = UserId
        });
        await dbContext.SaveChangesAsync();

        var logger = new NullLogger<AddMessageEvent.Handler>();
        var cut = new AddMessageEvent.Handler(this._mockDbContextFactory, logger);

        var command = new AddMessageEvent.Command
        {
            GuildId = GuildId,
            ChannelId = ChannelId,
            UserId = UserId,
            MessageId = MessageId1, // Duplicate MessageId
            MessageContent = "Duplicate message",
            Attachments = [],
            ParentChannelTree = []
        };

        var act = async () => await cut.Handle(command, default);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task WhenAttachmentProvided_AttachmentIsAdded()
    {
        await using var dbContext = this._createDbContext();

        var logger = new NullLogger<AddMessageEvent.Handler>();
        var cut = new AddMessageEvent.Handler(this._mockDbContextFactory, logger);

        var attachment = new AttachmentDto { Id = 1, FileName = "test.png" };

        await cut.Handle(
            new AddMessageEvent.Command
            {
                GuildId = GuildId,
                ChannelId = ChannelId,
                UserId = UserId,
                MessageId = MessageId1,
                MessageContent = "Test message with attachment",
                Attachments = [attachment],
                ParentChannelTree = []
            }, default);

        var message = await dbContext.Messages
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == MessageId1);
        message.Should().NotBeNull();
        message!.Attachments.Should().ContainSingle();
        message.Attachments.First().Id.Should().Be(attachment.Id);
        message.Attachments.First().FileName.Should().Be(attachment.FileName);
    }
}

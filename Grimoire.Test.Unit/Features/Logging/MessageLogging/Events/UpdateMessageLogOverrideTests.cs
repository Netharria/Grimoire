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
using Grimoire.Exceptions;
using Grimoire.Features.Logging.Settings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Grimoire.Test.Unit.Features.Logging.MessageLogging.Events;

[Collection("Test collection")]
public sealed class UpdateMessageLogOverrideTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong ChannelId = 1;

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
        await dbContext.AddAsync(new Channel { Id = ChannelId, GuildId = GuildId });
        await dbContext.SaveChangesAsync();

        this._mockDbContextFactory.CreateDbContextAsync().Returns(this._createDbContext());
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenUpdateMessageLogOverrideCalledWithAlwaysLog_AddAlwaysLogSetting()
    {
        await using var dbContext = this._createDbContext();
        var cut = new UpdateMessageLogOverride.Handler(this._mockDbContextFactory);

        var result = await cut.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = ChannelId,
                GuildId = GuildId,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Always
            }, default);

        dbContext.ChangeTracker.Clear();

        result.Message.Should().Be("Will now always log messages from <#1> and its sub channels/threads.");
        result.LogChannelId.Should().BeNull();

        var channelOverride = await dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == ChannelId);

        channelOverride.Should().NotBeNull();
        channelOverride?.ChannelOption.Should().Be(MessageLogOverrideOption.AlwaysLog);
    }

    [Fact]
    public async Task WhenUpdateMessageLogOverrideCalledWithNeverLog_AddNeverLogSetting()
    {
        await using var dbContext = this._createDbContext();
        var cut = new UpdateMessageLogOverride.Handler(this._mockDbContextFactory);

        var result = await cut.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = ChannelId,
                GuildId = GuildId,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Never
            }, default);

        dbContext.ChangeTracker.Clear();

        result.Message.Should().Be("Will now never log messages from <#1> and its sub channels/threads.");
        result.LogChannelId.Should().BeNull();

        var channelOverride = await dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == ChannelId);

        channelOverride.Should().NotBeNull();
        channelOverride?.ChannelOption.Should().Be(MessageLogOverrideOption.NeverLog);
    }

    [Fact]
    public async Task WhenUpdateMessageLogOverrideCalledWithNotImplementedOption_ThrowException()
    {
        var cut = new UpdateMessageLogOverride.Handler(this._mockDbContextFactory);

        await cut.Invoking(
                async x =>
                    await x.Handle(
                        new UpdateMessageLogOverride.Command
                        {
                            ChannelId = ChannelId,
                            GuildId = GuildId,
                            ChannelOverrideSetting = (UpdateMessageLogOverride.MessageLogOverrideSetting)5000
                        }, default))
            .Should()
            .ThrowAsync<NotImplementedException>()
            .WithMessage("A Message log Override option was selected that has not been implemented.");
    }

    [Fact]
    public async Task GivenGuildDoesntExist_WhenUpdateMessageLogOverrideCalled_ThrowException()
    {
        var cut = new UpdateMessageLogOverride.Handler(this._mockDbContextFactory);

        await cut.Invoking(
                async x =>
                    await x.Handle(
                        new UpdateMessageLogOverride.Command
                        {
                            ChannelId = ChannelId,
                            GuildId = 526546546546546,
                            ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Always
                        }, default))
            .Should()
            .ThrowAsync<AnticipatedException>()
            .WithMessage("Could not find guild settings.");
    }

    [Fact]
    public async Task GivenOverrideExists_WhenUpdateMessageLogOverrideCalledWithAlwaysLog_UpdateAlwaysLogSetting()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.MessagesLogChannelOverrides.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = ChannelId, GuildId = GuildId, ChannelOption = MessageLogOverrideOption.NeverLog
        });
        await dbContext.SaveChangesAsync();

        var cut = new UpdateMessageLogOverride.Handler(this._mockDbContextFactory);

        var result = await cut.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = ChannelId,
                GuildId = GuildId,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Always
            }, default);

        dbContext.ChangeTracker.Clear();

        result.Message.Should().Be("Will now always log messages from <#1> and its sub channels/threads.");
        result.LogChannelId.Should().BeNull();

        var channelOverride = await dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == ChannelId);

        channelOverride.Should().NotBeNull();
        channelOverride?.ChannelOption.Should().Be(MessageLogOverrideOption.AlwaysLog);
    }

    [Fact]
    public async Task GivenOverrideExists_WhenUpdateMessageLogOverrideCalledWithNeverLog_AddNeverLogSetting()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.MessagesLogChannelOverrides.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = ChannelId, GuildId = GuildId, ChannelOption = MessageLogOverrideOption.AlwaysLog
        });
        await dbContext.SaveChangesAsync();

        var cut = new UpdateMessageLogOverride.Handler(this._mockDbContextFactory);

        var result = await cut.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = ChannelId,
                GuildId = GuildId,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Never
            }, default);

        dbContext.ChangeTracker.Clear();

        result.Message.Should().Be("Will now never log messages from <#1> and its sub channels/threads.");
        result.LogChannelId.Should().BeNull();

        var channelOverride = await dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == ChannelId);

        channelOverride.Should().NotBeNull();
        channelOverride?.ChannelOption.Should().Be(MessageLogOverrideOption.NeverLog);
    }

    [Fact]
    public async Task GivenOverrideExists_WhenUpdateMessageLogOverrideCalledWithNotImplementedOption_ThrowException()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.MessagesLogChannelOverrides.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = ChannelId, GuildId = GuildId, ChannelOption = MessageLogOverrideOption.NeverLog
        });
        await dbContext.SaveChangesAsync();

        var cut = new UpdateMessageLogOverride.Handler(this._mockDbContextFactory);

        await cut.Invoking(
                async x =>
                    await x.Handle(
                        new UpdateMessageLogOverride.Command
                        {
                            ChannelId = ChannelId,
                            GuildId = GuildId,
                            ChannelOverrideSetting = (UpdateMessageLogOverride.MessageLogOverrideSetting)5000
                        }, default))
            .Should()
            .ThrowAsync<NotImplementedException>()
            .WithMessage("A Message log Override option was selected that has not been implemented.");
    }

    [Fact]
    public async Task GivenOverrideExists_WhenUpdateMessageLogOverrideCalledWithInherit_DeleteOverride()
    {
        await using var dbContext = this._createDbContext();
        await dbContext.MessagesLogChannelOverrides.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = ChannelId, GuildId = GuildId, ChannelOption = MessageLogOverrideOption.AlwaysLog
        });
        await dbContext.SaveChangesAsync();

        var cut = new UpdateMessageLogOverride.Handler(this._mockDbContextFactory);

        var result = await cut.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = ChannelId,
                GuildId = GuildId,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Inherit
            }, default);

        dbContext.ChangeTracker.Clear();

        result.Message.Should().Be("Override was successfully removed from the channel.");
        result.LogChannelId.Should().BeNull();

        var channelOverride = await dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == ChannelId);

        channelOverride.Should().BeNull();
    }

    [Fact]
    public async Task
        GivenOverrideDoesNotExists_WhenUpdateMessageLogOverrideCalledWithInherit_ThrowAnticipatedException()
    {
        await using var dbContext = this._createDbContext();
        var cut = new UpdateMessageLogOverride.Handler(this._mockDbContextFactory);

        await cut.Invoking(async x => await x.Handle(
                new UpdateMessageLogOverride.Command
                {
                    ChannelId = ChannelId,
                    GuildId = GuildId,
                    ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Inherit
                }, default))
            .Should()
            .ThrowAsync<AnticipatedException>()
            .WithMessage("That channel did not have an override.");

        dbContext.ChangeTracker.Clear();

        var channelOverride = await dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == ChannelId);

        channelOverride.Should().BeNull();
    }
}

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
using Grimoire.Features.MessageLogging.Commands;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.MessageLogging.Commands;

[Collection("Test collection")]
public sealed class UpdateMessageLogOverrideTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong CHANNEL_ID = 1;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.AddAsync(new Channel { Id = CHANNEL_ID, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenUpdateMessageLogOverrideCalledWithAlwaysLog_AddAlwaysLogSetting()
    {
        var cut = new UpdateMessageLogOverride.Handler(this._dbContext);

        var result = await cut.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = CHANNEL_ID,
                GuildId = GUILD_ID,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Always
            }, default);

        this._dbContext.ChangeTracker.Clear();

        result.Message.Should().Be("Will now always log messages from <#1> and its sub channels/threads.");
        result.LogChannelId.Should().BeNull();

        var channelOverride = await this._dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == CHANNEL_ID);

        channelOverride.Should().NotBeNull();
        channelOverride?.ChannelOption.Should().Be(MessageLogOverrideOption.AlwaysLog);
    }

    [Fact]
    public async Task WhenUpdateMessageLogOverrideCalledWithNeverLog_AddNeverLogSetting()
    {
        var cut = new UpdateMessageLogOverride.Handler(this._dbContext);

        var result = await cut.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = CHANNEL_ID,
                GuildId = GUILD_ID,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Never
            }, default);

        this._dbContext.ChangeTracker.Clear();

        result.Message.Should().Be("Will now never log messages from <#1> and its sub channels/threads.");
        result.LogChannelId.Should().BeNull();

        var channelOverride = await this._dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == CHANNEL_ID);

        channelOverride.Should().NotBeNull();
        channelOverride?.ChannelOption.Should().Be(MessageLogOverrideOption.NeverLog);
    }

    [Fact]
    public async Task WhenUpdateMessageLogOverrideCalledWithNotImplementedOption_ThrowException()
    {
        var cut = new UpdateMessageLogOverride.Handler(this._dbContext);

        await cut.Invoking(
            async x =>
                await x.Handle(
                new UpdateMessageLogOverride.Command
                {
                    ChannelId = CHANNEL_ID,
                    GuildId = GUILD_ID,
                    ChannelOverrideSetting = (UpdateMessageLogOverride.MessageLogOverrideSetting)5000
                }, default))
            .Should()
            .ThrowAsync<NotImplementedException>()
            .WithMessage("A Message log Override option was selected that has not been implemented.");
    }

    [Fact]
    public async Task GivenGuildDoesntExist_WhenUpdateMessageLogOverrideCalled_ThrowException()
    {
        var cut = new UpdateMessageLogOverride.Handler(this._dbContext);

        await cut.Invoking(
            async x =>
                await x.Handle(
                new UpdateMessageLogOverride.Command
                {
                    ChannelId = CHANNEL_ID,
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
        await this._dbContext.MessagesLogChannelOverrides.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = CHANNEL_ID,
            GuildId = GUILD_ID,
            ChannelOption = MessageLogOverrideOption.NeverLog
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new UpdateMessageLogOverride.Handler(this._dbContext);

        var result = await cut.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = CHANNEL_ID,
                GuildId = GUILD_ID,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Always
            }, default);

        this._dbContext.ChangeTracker.Clear();

        result.Message.Should().Be("Will now always log messages from <#1> and its sub channels/threads.");
        result.LogChannelId.Should().BeNull();

        var channelOverride = await this._dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == CHANNEL_ID);

        channelOverride.Should().NotBeNull();
        channelOverride?.ChannelOption.Should().Be(MessageLogOverrideOption.AlwaysLog);
    }

    [Fact]
    public async Task GivenOverrideExists_WhenUpdateMessageLogOverrideCalledWithNeverLog_AddNeverLogSetting()
    {
        await this._dbContext.MessagesLogChannelOverrides.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = CHANNEL_ID,
            GuildId = GUILD_ID,
            ChannelOption = MessageLogOverrideOption.AlwaysLog
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new UpdateMessageLogOverride.Handler(this._dbContext);

        var result = await cut.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = CHANNEL_ID,
                GuildId = GUILD_ID,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Never
            }, default);

        this._dbContext.ChangeTracker.Clear();

        result.Message.Should().Be("Will now never log messages from <#1> and its sub channels/threads.");
        result.LogChannelId.Should().BeNull();

        var channelOverride = await this._dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == CHANNEL_ID);

        channelOverride.Should().NotBeNull();
        channelOverride?.ChannelOption.Should().Be(MessageLogOverrideOption.NeverLog);
    }

    [Fact]
    public async Task GivenOverrideExists_WhenUpdateMessageLogOverrideCalledWithNotImplementedOption_ThrowException()
    {
        await this._dbContext.MessagesLogChannelOverrides.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = CHANNEL_ID,
            GuildId = GUILD_ID,
            ChannelOption = MessageLogOverrideOption.NeverLog
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new UpdateMessageLogOverride.Handler(this._dbContext);

        await cut.Invoking(
            async x =>
                await x.Handle(
                new UpdateMessageLogOverride.Command
                {
                    ChannelId = CHANNEL_ID,
                    GuildId = GUILD_ID,
                    ChannelOverrideSetting = (UpdateMessageLogOverride.MessageLogOverrideSetting)5000
                }, default))
            .Should()
            .ThrowAsync<NotImplementedException>()
            .WithMessage("A Message log Override option was selected that has not been implemented.");
    }

    [Fact]
    public async Task GivenOverrideExists_WhenUpdateMessageLogOverrideCalledWithInherit_DeleteOverride()
    {
        await this._dbContext.MessagesLogChannelOverrides.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = CHANNEL_ID,
            GuildId = GUILD_ID,
            ChannelOption = MessageLogOverrideOption.AlwaysLog
        });
        await this._dbContext.SaveChangesAsync();

        var cut = new UpdateMessageLogOverride.Handler(this._dbContext);

        var result = await cut.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = CHANNEL_ID,
                GuildId = GUILD_ID,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Inherit
            }, default);

        this._dbContext.ChangeTracker.Clear();

        result.Message.Should().Be("Override was successfully removed from the channel.");
        result.LogChannelId.Should().BeNull();

        var channelOverride = await this._dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == CHANNEL_ID);

        channelOverride.Should().BeNull();
    }

    [Fact]
    public async Task GivenOverrideDoesNotExists_WhenUpdateMessageLogOverrideCalledWithInherit_ThrowAnticipatedException()
    {

        var cut = new UpdateMessageLogOverride.Handler(this._dbContext);

        await cut.Invoking(async x => await x.Handle(
            new UpdateMessageLogOverride.Command
            {
                ChannelId = CHANNEL_ID,
                GuildId = GUILD_ID,
                ChannelOverrideSetting = UpdateMessageLogOverride.MessageLogOverrideSetting.Inherit
            }, default))
            .Should()
            .ThrowAsync<AnticipatedException>()
            .WithMessage("That channel did not have an override.");

        this._dbContext.ChangeTracker.Clear();

        var channelOverride = await this._dbContext.MessagesLogChannelOverrides
            .FirstOrDefaultAsync(x => x.ChannelId == CHANNEL_ID);

        channelOverride.Should().BeNull();
    }
}

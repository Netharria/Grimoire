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
using Grimoire.Features.Logging.Settings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Test.Unit.Features.MessageLogging.Queries;

[Collection("Test collection")]
public sealed class GetMessageLogOverrideTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong Channel1Id = 1;
    private const ulong Channel2Id = 2;

    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);

    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GuildId });
        await this._dbContext.AddAsync(new Channel { Id = Channel1Id, GuildId = GuildId });
        await this._dbContext.AddAsync(new Channel { Id = Channel2Id, GuildId = GuildId });
        await this._dbContext.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = Channel1Id, GuildId = GuildId, ChannelOption = MessageLogOverrideOption.AlwaysLog
        });
        await this._dbContext.AddAsync(new MessageLogChannelOverride
        {
            ChannelId = Channel2Id, GuildId = GuildId, ChannelOption = MessageLogOverrideOption.NeverLog
        });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenGetMessageLogOverrideCalled_ReturnOverrides()
    {
        var cut = new GetMessageLogOverrides.Handler(this._dbContext);

        var result = await cut.Handle(
            new GetMessageLogOverrides.Query { GuildId = GuildId }, default).ToListAsync();

        this._dbContext.ChangeTracker.Clear();

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
        var cut = new GetMessageLogOverrides.Handler(this._dbContext);

        var result = await cut.Handle(
            new GetMessageLogOverrides.Query { GuildId = 1321654 }, default).ToListAsync();

        this._dbContext.ChangeTracker.Clear();

        result.Should().NotBeNull().And
            .BeEmpty();
    }
}

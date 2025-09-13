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
using Grimoire.Features.Leveling.Settings;
using Grimoire.Features.Shared.SharedDtos;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Grimoire.Test.Unit.Features.Leveling.Settings;

[Collection("Test collection")]
public sealed class AddIgnoreForXpGainCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private const ulong GuildId = 1;
    private const ulong RoleId = 1;
    private const ulong ChannelId = 1;
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
        await dbContext.SaveChangesAsync();

        this._mockDbContextFactory.CreateDbContextAsync().Returns(this._createDbContext());
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenAddIgnoreForXpGainCommandHandlerCalled_AddIgnoreStatusAsync()
    {
        await using var dbContext = this._createDbContext();
        var cut = new AddIgnoreForXpGain.Handler(this._mockDbContextFactory);

        var result = await cut.Handle(
            new AddIgnoreForXpGain.Command
            {
                Users = [new UserDto { Id = UserId }],
                GuildId = GuildId,
                Channels =
                [
                    new ChannelDto { Id = ChannelId, GuildId = GuildId }
                ],
                Roles =
                [
                    new RoleDto { Id = RoleId, GuildId = GuildId }
                ]
            }, CancellationToken.None);

        var member = await dbContext.Members
            .Where(x =>
                x.UserId == UserId
                && x.GuildId == GuildId
            ).Include(member => member.IsIgnoredMember)
            .FirstAsync();

        member.IsIgnoredMember.Should().NotBeNull();

        var role = await dbContext.Roles
            .Where(x =>
                x.Id == RoleId
                && x.GuildId == GuildId
            ).Include(role => role.IsIgnoredRole)
            .FirstAsync();

        role.IsIgnoredRole.Should().NotBeNull();

        var channel = await dbContext.Channels
            .Where(x =>
                x.Id == ChannelId
                && x.GuildId == GuildId
            ).Include(channel => channel.IsIgnoredChannel)
            .FirstAsync();

        channel.IsIgnoredChannel.Should().NotBeNull();
    }
}

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
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Commands;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Grimoire.Core.Test.Unit.Features.Leveling.Commands;

[Collection("Test collection")]
public class AwardUserXpCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private readonly GrimoireDbContext _dbContext = new(
        new DbContextOptionsBuilder<GrimoireDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options);
    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;
    private const ulong GUILD_ID = 1;
    private const ulong USER_ID = 1;

    public async Task InitializeAsync()
    {
        await this._dbContext.AddAsync(new Guild { Id = GUILD_ID });
        await this._dbContext.AddAsync(new User { Id = USER_ID });
        await this._dbContext.AddAsync(new Member { UserId = USER_ID, GuildId = GUILD_ID });
        await this._dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => this._resetDatabase();

    [Fact]
    public async Task WhenAwardUserXpCommandHandlerCalled_UpdateMemebersXpAsync()
    {
        var cut = new AwardUserXpCommandHandler(this._dbContext);

        var result = await cut.Handle(
            new AwardUserXpCommand
            {
                UserId = USER_ID,
                GuildId = GUILD_ID,
                XpToAward = 20
            }, default);

        var member = await this._dbContext.Members.Where(x =>
            x.UserId == USER_ID
            && x.GuildId == GUILD_ID
            ).Include(x => x.XpHistory).FirstAsync();
        member.XpHistory.Sum(x => x.Xp).Should().Be(20);
    }

    [Fact]
    public async Task WhenAwardUserXpCommandHandlerCalled_WithMissingUser_ReturnFailedResponse()
    {

        var cut = new AwardUserXpCommandHandler(this._dbContext);
        var response = await Assert.ThrowsAsync<AnticipatedException>(async () => await cut.Handle(
            new AwardUserXpCommand
            {
                UserId = 20001,
                GuildId = GUILD_ID,
                XpToAward = 20
            }, default));

        response.Should().NotBeNull();
        response?.Message.Should().Be("<@!20001> was not found. Have they been on the server before?");
    }
}

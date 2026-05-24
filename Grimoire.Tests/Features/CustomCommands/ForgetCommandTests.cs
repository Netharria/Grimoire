// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Tests.Features.CustomCommands;

[Collection("Test collection")]
public sealed class ForgetCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly UserId _userId = new(10UL);
    private static readonly ModeratorId _moderatorId = new(10UL);

    private static readonly CustomCommandName _name =
        CustomCommandName.Create("wave").ShouldSucceed();

    private static readonly CustomCommandContent _content =
        CustomCommandContent.Create("👋").ShouldSucceed();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    /// <summary>
    ///     Mirrors the delete logic from <c>CustomCommandSettings.DeleteCommandAsync</c>.
    ///     Returns <see langword="true" /> when the command was already absent (alreadyForgotten).
    /// </summary>
    private async Task<bool> RunDeleteAsync(CustomCommandName name)
    {
        await using var db = factory.CreateDbContext();
        await db.CustomCommandUsages
            .Where(x => x.Name == name && x.GuildId == _guildId)
            .ExecuteDeleteAsync();
        var deletedCount = await db.CustomCommands
            .Where(x => x.Name == name && x.GuildId == _guildId)
            .ExecuteDeleteAsync();
        return deletedCount == 0;
    }

    [Fact]
    public async Task ForgetKnownCommand_DeletesCommand_ReturnsNotAlreadyForgotten()
    {
        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_name, _guildId, _content, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var alreadyForgotten = await RunDeleteAsync(_name);

        alreadyForgotten.ShouldBeFalse();
        await using var verifyDb = factory.CreateDbContext();
        (await verifyDb.CustomCommands
                .CountAsync(x => x.GuildId == _guildId && x.Name == _name, TestContext.Current.CancellationToken))
            .ShouldBe(0);
    }

    [Fact]
    public async Task ForgetUnknownCommand_ReturnsAlreadyForgotten()
    {
        var alreadyForgotten = await RunDeleteAsync(_name);

        alreadyForgotten.ShouldBeTrue();
    }

    [Fact]
    public async Task ForgetCommand_DeletesAllVersions()
    {
        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_name, _guildId, _content, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Task.Delay(10, TestContext.Current.CancellationToken);

        await using (var db = factory.CreateDbContext())
        {
            var content2 = CustomCommandContent.Create("updated").ShouldSucceed();
            await db.AddAsync(TextCustomCommand.Create(_name, _guildId, content2, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await RunDeleteAsync(_name);

        await using var verifyDb = factory.CreateDbContext();
        (await verifyDb.CustomCommands
                .CountAsync(x => x.GuildId == _guildId && x.Name == _name, TestContext.Current.CancellationToken))
            .ShouldBe(0);
    }

    [Fact]
    public async Task ForgetCommand_DeletesAssociatedUsages()
    {
        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_name, _guildId, _content, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.CustomCommandUsages.AddAsync(
                new CustomCommandUsage
                {
                    Name = _name,
                    GuildId = _guildId,
                    UserId = _userId,
                    UsedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                }, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await RunDeleteAsync(_name);

        await using var verifyDb = factory.CreateDbContext();
        (await verifyDb.CustomCommandUsages
                .CountAsync(x => x.GuildId == _guildId && x.Name == _name, TestContext.Current.CancellationToken))
            .ShouldBe(0);
    }

    [Fact]
    public async Task ForgetCommand_DoesNotAffectOtherCommands()
    {
        var otherName = CustomCommandName.Create("other").ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_name, _guildId, _content, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.AddAsync(TextCustomCommand.Create(otherName, _guildId, _content, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await RunDeleteAsync(_name);

        await using var verifyDb = factory.CreateDbContext();
        (await verifyDb.CustomCommands
                .CountAsync(x => x.GuildId == _guildId && x.Name == otherName, TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }
}

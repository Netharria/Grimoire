// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Tests.Features.CustomCommands;

/// <summary>
///     Tests for <see cref="CustomCommandDatabaseQueryHelpers.GetCustomCommandQuery" /> —
///     the "latest version wins" filter used by both slash-command lookup and
///     the text-command processor.
/// </summary>
[Collection("Test collection")]
public sealed class AutocompleteQueryTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _moderatorId = new(1UL);
    private static readonly CustomCommandName _wave = CustomCommandName.Create("wave").ShouldSucceed();
    private static readonly CustomCommandName _greet = CustomCommandName.Create("greet").ShouldSucceed();

    private static readonly CustomCommandContent _v1Content =
        CustomCommandContent.Create("version one").ShouldSucceed();

    private static readonly CustomCommandContent _v2Content =
        CustomCommandContent.Create("version two — current").ShouldSucceed();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    [Fact]
    public async Task GetCustomCommandQuery_NoCommand_ReturnsNull()
    {
        await using var db = factory.CreateDbContext();

        var result = await db.CustomCommands
            .GetCustomCommandQuery(_guildId, _wave)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCustomCommandQuery_SingleVersion_ReturnsIt()
    {
        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_wave, _guildId, _v1Content, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var queryDb = factory.CreateDbContext();
        var result = await queryDb.CustomCommands
            .GetCustomCommandQuery(_guildId, _wave)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Content.Value.ShouldBe(_v1Content.Value);
    }

    [Fact]
    public async Task GetCustomCommandQuery_TwoVersions_ReturnsOnlyLatest()
    {
        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_wave, _guildId, _v1Content, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Task.Delay(10, TestContext.Current.CancellationToken); // distinct CreatedAt

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_wave, _guildId, _v2Content, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var queryDb = factory.CreateDbContext();
        var results = await queryDb.CustomCommands
            .GetCustomCommandQuery(_guildId, _wave)
            .ToListAsync(TestContext.Current.CancellationToken);

        results.ShouldHaveSingleItem();
        results[0].Content.Value.ShouldBe(_v2Content.Value);
    }

    [Fact]
    public async Task GetCustomCommandQuery_IncludesRoles()
    {
        var roleId = new RoleId(42UL);
        ICollection<CustomCommandRole> roles =
        [
            new CustomCommandAllowRole { RoleId = roleId, Name = _wave, GuildId = _guildId, CreatedAt = default }
        ];

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_wave, _guildId, _v1Content, roles, _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var queryDb = factory.CreateDbContext();
        var result = await queryDb.CustomCommands
            .GetCustomCommandQuery(_guildId, _wave)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Roles.ShouldHaveSingleItem().ShouldBeOfType<CustomCommandAllowRole>()
            .RoleId.ShouldBe(roleId);
    }

    [Fact]
    public async Task GetCustomCommandQuery_WrongGuild_ReturnsNull()
    {
        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_wave, new GuildId(2UL), _v1Content, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var queryDb = factory.CreateDbContext();
        var result = await queryDb.CustomCommands
            .GetCustomCommandQuery(_guildId, _wave)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCustomCommandQuery_TwoCommandsSameGuild_QueriedIndependently()
    {
        var waveContent = CustomCommandContent.Create("wave!").ShouldSucceed();
        var greetContent = CustomCommandContent.Create("hello!").ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_wave, _guildId, waveContent, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.AddAsync(TextCustomCommand.Create(_greet, _guildId, greetContent, [], _moderatorId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var queryDb = factory.CreateDbContext();
        var wave = await queryDb.CustomCommands.GetCustomCommandQuery(_guildId, _wave)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        var greet = await queryDb.CustomCommands.GetCustomCommandQuery(_guildId, _greet)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        wave.ShouldNotBeNull();
        wave.Content.Value.ShouldBe("wave!");
        greet.ShouldNotBeNull();
        greet.Content.Value.ShouldBe("hello!");
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Tests.Features.CustomCommands;

[Collection("Test collection")]
public sealed class LearnCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);

    private static readonly CustomCommandName _name =
        CustomCommandName.Create("greet").ShouldSucceed();

    private static readonly CustomCommandContent _content =
        CustomCommandContent.Create("Hello %Mention! %Message").ShouldSucceed();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    [Fact]
    public async Task LearnTextCommand_Saved_CanBeRetrieved()
    {
        var cmd = TextCustomCommand.Create(_name, _guildId, _content, [], null).ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(cmd, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verifyDb = factory.CreateDbContext();
        var saved = await verifyDb.CustomCommands
            .AsNoTracking()
            .SingleAsync(x => x.GuildId == _guildId && x.Name == _name, TestContext.Current.CancellationToken);
        saved.ShouldBeOfType<TextCustomCommand>();
        saved.Content.Value.ShouldBe(_content.Value);
    }

    [Fact]
    public async Task LearnEmbedCommand_WithColor_Saved_CanBeRetrieved()
    {
        var color = CustomCommandEmbedColor.Create("FF5500").ShouldSucceed();
        var cmd = EmbedCustomCommand.Create(_name, _guildId, _content, color, [], null).ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(cmd, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verifyDb = factory.CreateDbContext();
        var saved = await verifyDb.CustomCommands
            .AsNoTracking()
            .SingleAsync(x => x.GuildId == _guildId && x.Name == _name, TestContext.Current.CancellationToken);
        var embed = saved.ShouldBeOfType<EmbedCustomCommand>();
        embed.EmbedColor.ShouldNotBeNull();
        embed.EmbedColor!.Value.Value.ShouldBe("FF5500");
    }

    [Fact]
    public async Task LearnEmbedCommand_WithoutColor_Saved_EmbedColorIsNull()
    {
        var cmd = EmbedCustomCommand.Create(_name, _guildId, _content, null, [], null).ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(cmd, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verifyDb = factory.CreateDbContext();
        var saved = await verifyDb.CustomCommands
            .AsNoTracking()
            .SingleAsync(x => x.GuildId == _guildId && x.Name == _name, TestContext.Current.CancellationToken);
        saved.ShouldBeOfType<EmbedCustomCommand>().EmbedColor.ShouldBeNull();
    }

    [Fact]
    public async Task LearnCommandWithAllowRole_RolePersistedCorrectly()
    {
        var roleId = new RoleId(42UL);
        ICollection<CustomCommandRole> roles =
        [
            new CustomCommandAllowRole { RoleId = roleId, Name = _name, GuildId = _guildId, CreatedAt = default }
        ];
        var cmd = TextCustomCommand.Create(_name, _guildId, _content, roles, null).ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(cmd, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verifyDb = factory.CreateDbContext();
        var saved = await verifyDb.CustomCommands
            .AsNoTracking()
            .Include(x => x.Roles)
            .SingleAsync(x => x.GuildId == _guildId && x.Name == _name, TestContext.Current.CancellationToken);
        saved.Roles.ShouldHaveSingleItem().ShouldBeOfType<CustomCommandAllowRole>()
            .RoleId.ShouldBe(roleId);
    }

    [Fact]
    public async Task LearnCommandTwice_BothVersionsPersisted()
    {
        var cmd1 = TextCustomCommand.Create(_name, _guildId, _content, [], null).ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(cmd1, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Task.Delay(10, TestContext.Current.CancellationToken); // ensure distinct CreatedAt

        var content2 = CustomCommandContent.Create("Updated content").ShouldSucceed();
        var cmd2 = TextCustomCommand.Create(_name, _guildId, content2, [], null).ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(cmd2, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verifyDb = factory.CreateDbContext();
        var count = await verifyDb.CustomCommands
            .CountAsync(x => x.GuildId == _guildId && x.Name == _name, TestContext.Current.CancellationToken);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task LearnCommand_ModeratorId_Persisted()
    {
        var modId = new ModeratorId(99UL);
        var cmd = TextCustomCommand.Create(_name, _guildId, _content, [], modId).ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(cmd, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verifyDb = factory.CreateDbContext();
        var saved = await verifyDb.CustomCommands
            .AsNoTracking()
            .SingleAsync(x => x.GuildId == _guildId && x.Name == _name, TestContext.Current.CancellationToken);
        saved.ModeratorId.ShouldBe(modId);
    }
}

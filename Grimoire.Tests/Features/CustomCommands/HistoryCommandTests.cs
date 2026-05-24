// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Tests.Features.CustomCommands;

[Collection("Test collection")]
public sealed class HistoryCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);

    private static readonly CustomCommandName _name =
        CustomCommandName.Create("history").ShouldSucceed();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    /// <summary>
    ///     Mirrors the query from <c>CustomCommandSettings.GetVersionsAsync</c>.
    /// </summary>
    private async Task<List<(DateTimeOffset CreatedAt, ModeratorId? ModeratorId, CustomCommandContent Content)>>
        QueryVersionsAsync(CustomCommandName name)
    {
        await using var db = factory.CreateDbContext();
        return (await db.CustomCommands
                .AsNoTracking()
                .Where(x => x.GuildId == _guildId && x.Name == name)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new { x.CreatedAt, x.ModeratorId, x.Content })
                .ToListAsync())
            .Select(v => (v.CreatedAt, v.ModeratorId, v.Content))
            .ToList();
    }

    [Fact]
    public async Task History_UnknownCommand_ReturnsEmpty()
    {
        var versions = await QueryVersionsAsync(_name);

        versions.ShouldBeEmpty();
    }

    [Fact]
    public async Task History_SingleVersion_ReturnsIt()
    {
        var content = CustomCommandContent.Create("v1 content").ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_name, _guildId, content, [], _modId).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var versions = await QueryVersionsAsync(_name);

        versions.ShouldHaveSingleItem();
        versions[0].Content.Value.ShouldBe("v1 content");
        versions[0].ModeratorId.ShouldBe(_modId);
    }

    [Fact]
    public async Task History_MultipleVersions_ReturnedNewestFirst()
    {
        foreach (var text in new[] { "v1", "v2", "v3 current" })
        {
            var c = CustomCommandContent.Create(text).ShouldSucceed();
            await using var db = factory.CreateDbContext();
            await db.AddAsync(TextCustomCommand.Create(_name, _guildId, c, [], null).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Task.Delay(10, TestContext.Current.CancellationToken); // distinct timestamps
        }

        var versions = await QueryVersionsAsync(_name);

        versions.Count.ShouldBe(3);
        versions[0].Content.Value.ShouldBe("v3 current");
        versions[1].Content.Value.ShouldBe("v2");
        versions[2].Content.Value.ShouldBe("v1");
    }

    [Fact]
    public async Task History_SameNameDifferentGuild_GuildIsolated()
    {
        var otherGuildId = new GuildId(2UL);
        var content1 = CustomCommandContent.Create("guild1 cmd").ShouldSucceed();
        var content2 = CustomCommandContent.Create("guild2 cmd").ShouldSucceed();

        await using (var db = factory.CreateDbContext())
        {
            await db.AddAsync(TextCustomCommand.Create(_name, _guildId, content1, [], null).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.AddAsync(TextCustomCommand.Create(_name, otherGuildId, content2, [], null).ShouldSucceed(),
                TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var versions = await QueryVersionsAsync(_name);

        versions.ShouldHaveSingleItem();
        versions[0].Content.Value.ShouldBe("guild1 cmd");
    }
}

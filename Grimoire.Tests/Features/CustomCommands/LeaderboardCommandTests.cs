// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Tests.Features.CustomCommands;

[Collection("Test collection")]
public sealed class LeaderboardCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly UserId _user1 = new(10UL);
    private static readonly UserId _user2 = new(20UL);
    private static readonly UserId _user3 = new(30UL);

    private static readonly CustomCommandName _wave = CustomCommandName.Create("wave").ShouldSucceed();
    private static readonly CustomCommandName _greet = CustomCommandName.Create("greet").ShouldSucceed();
    private static readonly CustomCommandName _bye = CustomCommandName.Create("bye").ShouldSucceed();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    /// <summary>Mirrors <c>CustomCommandSettings.GetOverallLeaderboardAsync</c>.</summary>
    private async Task<List<(CustomCommandName Name, int Count)>> QueryOverallLeaderboardAsync()
    {
        await using var db = factory.CreateDbContext();
        return (await db.CustomCommandUsages
                .Where(x => x.GuildId == _guildId)
                .GroupBy(x => x.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(15)
                .ToListAsync())
            .Select(r => (r.Name, r.Count))
            .ToList();
    }

    /// <summary>Mirrors <c>CustomCommandSettings.GetCommandLeaderboardAsync</c>.</summary>
    private async Task<List<(UserId UserId, int Count)>> QueryCommandLeaderboardAsync(CustomCommandName name)
    {
        await using var db = factory.CreateDbContext();
        return (await db.CustomCommandUsages
                .Where(x => x.GuildId == _guildId && x.Name == name)
                .GroupBy(x => x.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(15)
                .ToListAsync())
            .Select(r => (r.UserId, r.Count))
            .ToList();
    }

    private async Task SeedUsagesAsync(CustomCommandName name, UserId userId, int count)
    {
        await using var db = factory.CreateDbContext();
        var usages = Enumerable.Range(0, count).Select(i => new CustomCommandUsage
        {
            Name = name, GuildId = _guildId, UserId = userId, UsedAt = DateTimeOffset.UtcNow.AddMinutes(-i - 1)
        });
        await db.CustomCommandUsages.AddRangeAsync(usages);
        await db.SaveChangesAsync();
    }

    // ── Overall leaderboard ───────────────────────────────────────────────────

    [Fact]
    public async Task Overall_NoUsages_ReturnsEmpty()
    {
        var rankings = await QueryOverallLeaderboardAsync();

        rankings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Overall_CommandsRankedByTotalDescending()
    {
        await SeedUsagesAsync(_wave, _user1, 5);
        await SeedUsagesAsync(_greet, _user1, 3);
        await SeedUsagesAsync(_bye, _user2, 1);

        var rankings = await QueryOverallLeaderboardAsync();

        rankings.Count.ShouldBe(3);
        rankings[0].Name.ShouldBe(_wave);
        rankings[0].Count.ShouldBe(5);
        rankings[1].Name.ShouldBe(_greet);
        rankings[1].Count.ShouldBe(3);
        rankings[2].Name.ShouldBe(_bye);
        rankings[2].Count.ShouldBe(1);
    }

    [Fact]
    public async Task Overall_UsagesFromMultipleUsers_AggregatedPerCommand()
    {
        // wave: user1×2 + user2×3 = 5 total; greet: user3×4 = 4 total
        await SeedUsagesAsync(_wave, _user1, 2);
        await SeedUsagesAsync(_wave, _user2, 3);
        await SeedUsagesAsync(_greet, _user3, 4);

        var rankings = await QueryOverallLeaderboardAsync();

        rankings.Count.ShouldBe(2);
        rankings[0].Name.ShouldBe(_wave);
        rankings[0].Count.ShouldBe(5);
        rankings[1].Name.ShouldBe(_greet);
        rankings[1].Count.ShouldBe(4);
    }

    [Fact]
    public async Task Overall_OtherGuildUsages_NotIncluded()
    {
        await SeedUsagesAsync(_wave, _user1, 5);

        await using (var db = factory.CreateDbContext())
        {
            var otherGuildUsages = Enumerable.Range(0, 10).Select(i => new CustomCommandUsage
            {
                Name = _wave,
                GuildId = new GuildId(2UL),
                UserId = _user2,
                UsedAt = DateTimeOffset.UtcNow.AddMinutes(-i - 1)
            });
            await db.CustomCommandUsages.AddRangeAsync(otherGuildUsages, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var rankings = await QueryOverallLeaderboardAsync();

        rankings.ShouldHaveSingleItem();
        rankings[0].Count.ShouldBe(5);
    }

    // ── Per-command leaderboard ───────────────────────────────────────────────

    [Fact]
    public async Task PerCommand_NoUsages_ReturnsEmpty()
    {
        var rankings = await QueryCommandLeaderboardAsync(_wave);

        rankings.ShouldBeEmpty();
    }

    [Fact]
    public async Task PerCommand_UsersRankedByCountDescending()
    {
        await SeedUsagesAsync(_wave, _user1, 4);
        await SeedUsagesAsync(_wave, _user2, 2);
        await SeedUsagesAsync(_wave, _user3, 1);

        var rankings = await QueryCommandLeaderboardAsync(_wave);

        rankings.Count.ShouldBe(3);
        rankings[0].UserId.ShouldBe(_user1);
        rankings[0].Count.ShouldBe(4);
        rankings[1].UserId.ShouldBe(_user2);
        rankings[1].Count.ShouldBe(2);
        rankings[2].UserId.ShouldBe(_user3);
        rankings[2].Count.ShouldBe(1);
    }

    [Fact]
    public async Task PerCommand_OtherCommandUsages_NotIncluded()
    {
        await SeedUsagesAsync(_wave, _user1, 3);
        await SeedUsagesAsync(_greet, _user2, 10);

        var rankings = await QueryCommandLeaderboardAsync(_wave);

        rankings.ShouldHaveSingleItem();
        rankings[0].UserId.ShouldBe(_user1);
        rankings[0].Count.ShouldBe(3);
    }
}

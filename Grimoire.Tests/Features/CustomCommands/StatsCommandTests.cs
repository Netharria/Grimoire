// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Tests.Features.CustomCommands;

[Collection("Test collection")]
public sealed class StatsCommandTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly UserId _user1 = new(10UL);
    private static readonly UserId _user2 = new(20UL);
    private static readonly UserId _user3 = new(30UL);

    private static readonly CustomCommandName _name =
        CustomCommandName.Create("stats").ShouldSucceed();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    /// <summary>
    ///     Mirrors <c>CustomCommandSettings.FetchUsageStatsAsync</c>.
    /// </summary>
    private async Task<(int Total, int LastMonth, int LastYear, DateTimeOffset? LastUsed)?> QueryUsageStatsAsync(
        DateTimeOffset now)
    {
        await using var db = factory.CreateDbContext();
        var raw = await db.CustomCommandUsages
            .Where(x => x.GuildId == _guildId && x.Name == _name)
            .GroupBy(_ => true)
            .Select(g => new
            {
                Total = g.Count(),
                LastMonth = g.Count(x => x.UsedAt > now.AddDays(-30)),
                LastYear = g.Count(x => x.UsedAt > now.AddDays(-365)),
                LastUsed = g.Max(x => (DateTimeOffset?)x.UsedAt)
            })
            .FirstOrDefaultAsync();
        return raw is null ? null : (raw.Total, raw.LastMonth, raw.LastYear, raw.LastUsed);
    }

    /// <summary>
    ///     Mirrors <c>CustomCommandSettings.FetchTopUsersAsync</c>.
    /// </summary>
    private async Task<List<(UserId UserId, int Count)>> QueryTopUsersAsync()
    {
        await using var db = factory.CreateDbContext();
        return (await db.CustomCommandUsages
                .Where(x => x.GuildId == _guildId && x.Name == _name)
                .GroupBy(x => x.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(3)
                .ToListAsync())
            .Select(u => (u.UserId, u.Count))
            .ToList();
    }

    private async Task SeedUsageAsync(UserId userId, DateTimeOffset usedAt)
    {
        await using var db = factory.CreateDbContext();
        await db.CustomCommandUsages.AddAsync(new CustomCommandUsage
        {
            Name = _name, GuildId = _guildId, UserId = userId, UsedAt = usedAt
        });
        await db.SaveChangesAsync();
    }

    // ── FetchUsageStatsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task Stats_NoUsages_ReturnsNull()
    {
        var stats = await QueryUsageStatsAsync(DateTimeOffset.UtcNow);

        stats.ShouldBeNull();
    }

    [Fact]
    public async Task Stats_WithUsages_TotalIsCorrect()
    {
        var now = DateTimeOffset.UtcNow;
        await SeedUsageAsync(_user1, now.AddDays(-1));
        await SeedUsageAsync(_user2, now.AddDays(-2));
        await SeedUsageAsync(_user3, now.AddDays(-3));

        var stats = await QueryUsageStatsAsync(now);

        stats.ShouldNotBeNull();
        stats.Value.Total.ShouldBe(3);
    }

    [Fact]
    public async Task Stats_UsagesAtVariousTimes_BucketCountsCorrect()
    {
        var now = DateTimeOffset.UtcNow;
        await SeedUsageAsync(_user1, now.AddDays(-10)); // within last 30 days
        await SeedUsageAsync(_user2, now.AddDays(-60)); // within last year, not 30 days
        await SeedUsageAsync(_user3, now.AddDays(-400)); // older than a year

        var stats = await QueryUsageStatsAsync(now);

        stats.ShouldNotBeNull();
        stats.Value.Total.ShouldBe(3);
        stats.Value.LastMonth.ShouldBe(1);
        stats.Value.LastYear.ShouldBe(2);
    }

    [Fact]
    public async Task Stats_LastUsed_ReflectsNewestUsage()
    {
        var now = DateTimeOffset.UtcNow;
        var recentTime = now.AddHours(-1);
        await SeedUsageAsync(_user1, now.AddDays(-10));
        await SeedUsageAsync(_user2, recentTime);

        var stats = await QueryUsageStatsAsync(now);

        stats.ShouldNotBeNull();
        stats.Value.LastUsed.ShouldNotBeNull();
        (stats.Value.LastUsed!.Value - recentTime).Duration()
            .ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Stats_OtherGuild_NotIncludedInCount()
    {
        var now = DateTimeOffset.UtcNow;
        await SeedUsageAsync(_user1, now.AddDays(-1));

        await using (var db = factory.CreateDbContext())
        {
            await db.CustomCommandUsages.AddAsync(
                new CustomCommandUsage
                {
                    Name = _name, GuildId = new GuildId(2UL), UserId = _user2, UsedAt = now.AddDays(-2)
                }, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var stats = await QueryUsageStatsAsync(now);

        stats.ShouldNotBeNull();
        stats.Value.Total.ShouldBe(1);
    }

    // ── FetchTopUsersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task TopUsers_NoUsages_ReturnsEmpty()
    {
        var topUsers = await QueryTopUsersAsync();

        topUsers.ShouldBeEmpty();
    }

    [Fact]
    public async Task TopUsers_RankedByUsageDescending()
    {
        var now = DateTimeOffset.UtcNow;
        // user1: 3 uses, user2: 2 uses, user3: 1 use
        for (var i = 0; i < 3; i++) await SeedUsageAsync(_user1, now.AddMinutes(-30 - i));
        for (var i = 0; i < 2; i++) await SeedUsageAsync(_user2, now.AddMinutes(-20 - i));
        await SeedUsageAsync(_user3, now.AddMinutes(-5));

        var topUsers = await QueryTopUsersAsync();

        topUsers.Count.ShouldBe(3);
        topUsers[0].UserId.ShouldBe(_user1);
        topUsers[0].Count.ShouldBe(3);
        topUsers[1].UserId.ShouldBe(_user2);
        topUsers[1].Count.ShouldBe(2);
        topUsers[2].UserId.ShouldBe(_user3);
        topUsers[2].Count.ShouldBe(1);
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Leveling.UserCommands;

namespace Grimoire.Tests.Features.Leveling.UserCommands;

[Collection("Test collection")]
public sealed class GetLeaderboardTests(GrimoireCoreFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly GuildId _otherGuildId = new(2UL);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // Respawn truncates the underlying tables but leaves the materialized view's last snapshot in
    // place, so it must be refreshed back to empty here too or the next test starts with stale rows.
    public async ValueTask DisposeAsync()
    {
        await factory.ResetDatabase();
        await this.RefreshLeaderboardViewAsync();
    }

    private GetLeaderboard CreateSut() => new(factory.CreateDbContextFactory(), null!);

    private async Task SeedXpAsync(UserId userId, GuildId guildId, long xp)
    {
        var earned = EarnedXp.Create(PositiveXpAmount.Create(xp).ShouldSucceed(), userId, guildId, DateTimeOffset.UtcNow)
            .ShouldSucceed();

        await using var db = factory.CreateDbContext();
        await db.XpHistory.AddAsync(earned);
        await db.SaveChangesAsync();
    }

    /// <summary>leaderboard_view is a materialized view and must be refreshed after seeding XpHistory.</summary>
    private async Task RefreshLeaderboardViewAsync()
    {
        await using var db = factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW leaderboard_view");
    }

    // ── GetTopLeaderboardAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetTopLeaderboardAsync_RanksUsersByXpDescending()
    {
        await SeedXpAsync(new UserId(1UL), _guildId, 100);
        await SeedXpAsync(new UserId(2UL), _guildId, 300);
        await SeedXpAsync(new UserId(3UL), _guildId, 200);
        await RefreshLeaderboardViewAsync();

        var response = await CreateSut().GetTopLeaderboardAsync(_guildId, TestContext.Current.CancellationToken);

        var value = response.ShouldSucceed();
        value.TotalUserCount.ShouldBe(3);
        var lines = value.LeaderboardText.Split('\n');
        lines[0].ShouldStartWith("**1**");
        lines[0].ShouldContain("<@2>");
        lines[1].ShouldContain("<@3>");
        lines[2].ShouldContain("<@1>");
    }

    [Fact]
    public async Task GetTopLeaderboardAsync_MoreThanFifteenUsers_ReturnsOnlyTopFifteen_ButFullTotalCount()
    {
        for (var i = 1; i <= 18; i++)
            await SeedXpAsync(new UserId((ulong)i), _guildId, i * 10);
        await RefreshLeaderboardViewAsync();

        var response = await CreateSut().GetTopLeaderboardAsync(_guildId, TestContext.Current.CancellationToken);

        var value = response.ShouldSucceed();
        value.TotalUserCount.ShouldBe(18);
        value.LeaderboardText.Split('\n').Length.ShouldBe(15);
    }

    [Fact]
    public async Task GetTopLeaderboardAsync_OnlyCountsUsersInRequestedGuild()
    {
        await SeedXpAsync(new UserId(1UL), _guildId, 100);
        await SeedXpAsync(new UserId(2UL), _otherGuildId, 999);
        await RefreshLeaderboardViewAsync();

        var response = await CreateSut().GetTopLeaderboardAsync(_guildId, TestContext.Current.CancellationToken);

        response.ShouldSucceed().TotalUserCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetTopLeaderboardAsync_NoUsers_ReturnsEmptyTextAndZeroCount()
    {
        var response = await CreateSut().GetTopLeaderboardAsync(_guildId, TestContext.Current.CancellationToken);

        var value = response.ShouldSucceed();
        value.TotalUserCount.ShouldBe(0);
        value.LeaderboardText.ShouldBe(string.Empty);
    }

    // ── GetUserCenteredLeaderboardAsync ──────────────────────────────────────

    [Fact]
    public async Task GetUserCenteredLeaderboardAsync_UserNotOnLeaderboard_ReturnsNotFound()
    {
        var response = await CreateSut().GetUserCenteredLeaderboardAsync(
            _guildId, new UserId(999UL), TestContext.Current.CancellationToken);

        response.ShouldBeOfType<Result<GetLeaderboard.Response>.NotFound>()
            .Error.Code.ShouldBe("leaderboard.user-not-found");
    }

    [Fact]
    public async Task GetUserCenteredLeaderboardAsync_ReturnsWindowAroundUser_AndFullTotalCount()
    {
        // Users 1..20 with xp = i * 10, so higher id == higher xp == better rank.
        // User 10 (xp 100) has 10 users ranked above it, so its rank is 11.
        // Window is [rank-5, rank+9] = [6, 20], which is 15 users (clamped by data at the bottom).
        for (var i = 1; i <= 20; i++)
            await SeedXpAsync(new UserId((ulong)i), _guildId, i * 10);
        await RefreshLeaderboardViewAsync();

        var response = await CreateSut().GetUserCenteredLeaderboardAsync(
            _guildId, new UserId(10UL), TestContext.Current.CancellationToken);

        var value = response.ShouldSucceed();
        value.TotalUserCount.ShouldBe(20);
        value.LeaderboardText.ShouldContain("<@10>");
        value.LeaderboardText.Split('\n').Length.ShouldBe(15);
    }

    [Fact]
    public async Task GetUserCenteredLeaderboardAsync_OtherGuildUser_ReturnsNotFound()
    {
        await SeedXpAsync(new UserId(1UL), _otherGuildId, 100);
        await RefreshLeaderboardViewAsync();

        var response = await CreateSut().GetUserCenteredLeaderboardAsync(
            _guildId, new UserId(1UL), TestContext.Current.CancellationToken);

        response.ShouldBeOfType<Result<GetLeaderboard.Response>.NotFound>();
    }
}

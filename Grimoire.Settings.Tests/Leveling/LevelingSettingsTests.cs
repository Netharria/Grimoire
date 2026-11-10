// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Tests.Leveling;

[Collection("Settings collection")]
public sealed class LevelingSettingsTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    [Fact]
    public async Task NoRows_ReturnsAllDefaults()
    {
        var settings = await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        settings.XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromMinutes(3));
        settings.Base.Value.ShouldBe(15);
        settings.Modifier.Value.ShouldBe(50);
        settings.Amount.Value.ShouldBe(5);
    }

    /// <summary>
    ///     S-2: a failed lookup is reported as a failure and isn't cached. Caching the defaults would give the guild
    ///     the wrong XP amount, timeout and level curve for a day.
    /// </summary>
    /// <remarks>Cases: XC-106.</remarks>
    [Fact]
    public async Task ALookupDuringAnOutage_Fails_AndIsNotCached()
    {
        (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 42,
            TestContext.Current.CancellationToken)).ShouldSucceed();
        var down = true;
        var sut = SettingsModuleFactory.Create(factory.ConnectionString, () => down);

        (await sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken))
            .ShouldBeOfType<Result<LevelingSettingEntry>.Invalid>();

        down = false;
        (await sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .Amount.Value.ShouldBe(42);
    }

    /// <summary>
    ///     X-9: a level is the one whose threshold the XP has reached and whose next threshold it hasn't. A shortcut
    ///     used to start past the answer for small Base and Modifier, by 24 levels at Base 1 and Modifier 1.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(15, 1)]
    [InlineData(15, 50)]
    public async Task GetLevelFromXp_IsExact_ForSmallBaseAndModifier(int levelBase, int modifier)
    {
        (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, levelBase, TestContext.Current.CancellationToken)).ShouldSucceed();
        (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, modifier, TestContext.Current.CancellationToken)).ShouldSucceed();
        var settings = (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed();

        foreach (var xp in new long[] { 0, 1, 999, 1_001, 5_000, 25_000, 100_000, 2_000_000 })
        {
            var level = settings.GetLevelFromXp(xp);
            settings.GetXpNeededForLevel(level).ShouldBeLessThanOrEqualTo(xp, $"level {level} at {xp} XP");
            settings.GetXpNeededForLevel(level, 1).ShouldBeGreaterThan(xp, $"level {level} at {xp} XP");
        }
    }

    /// <summary>The search gives the same level as counting up one level at a time, which is the definition.</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 200)]
    [InlineData(15, 50)]
    [InlineData(37, 13)]
    [InlineData(500, 1)]
    [InlineData(500, 200)]
    public async Task GetLevelFromXp_MatchesCountingUp(int levelBase, int modifier)
    {
        (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, levelBase, TestContext.Current.CancellationToken)).ShouldSucceed();
        (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, modifier, TestContext.Current.CancellationToken)).ShouldSucceed();
        var settings = (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed();

        foreach (var xp in Enumerable.Range(0, 400).Select(i => (long)i * i * 7).Append(2_000_000))
            settings.GetLevelFromXp(xp).ShouldBe(CountUp(levelBase, modifier, xp), $"at {xp} XP");
    }

    /// <summary>
    ///     A huge award, as once froze the old Python bot on every level check, is answered at once. With the
    ///     smallest settings, counting up would take billions of steps.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(15, 50)]
    public async Task GetLevelFromXp_ForAnAbsurdAmount_ReturnsAtOnce(int levelBase, int modifier)
    {
        (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, levelBase, TestContext.Current.CancellationToken)).ShouldSucceed();
        (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, modifier, TestContext.Current.CancellationToken)).ShouldSucceed();
        var settings = (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var xp in new[] { 1_000_000_000_000L, long.MaxValue / 2, long.MaxValue })
        {
            var level = settings.GetLevelFromXp(xp);
            level.ShouldBeGreaterThan(1);
            if (level < int.MaxValue)
                settings.GetXpNeededForLevel(level).ShouldBeLessThanOrEqualTo(xp);
        }

        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(100));
    }

    /// <summary>The definition: count thresholds one at a time. Only for amounts small enough to count.</summary>
    private static int CountUp(int levelBase, int modifier, long xp)
    {
        for (var i = 0; ; i++)
            if (xp < levelBase + (long)Math.Round(levelBase * (modifier / 100.0) * i) * i)
                return i + 1;
    }

    /// <summary>
    ///     S-1: a value main allowed but today's limits don't, such as a Base of 1000 copied in by the migration,
    ///     is kept. Rejecting it on read made every lookup fail and stopped leveling for the whole guild.
    /// </summary>
    [Fact]
    public async Task ALegacyValueOutsideTodaysLimits_IsKept()
    {
        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.GuildSettings.Add(GuildSettingCustomValue.Create(GuildSettingType.LevelScalingBase, _guildId, _modId,
                DateTimeOffset.UtcNow, "1000").ShouldSucceed());
            dbContext.GuildSettings.Add(GuildSettingCustomValue.Create(GuildSettingType.XpTimeoutPeriod, _guildId, _modId,
                DateTimeOffset.UtcNow, "25:00:00").ShouldSucceed());
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var settings = (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed();

        settings.Base.Value.ShouldBe(1000);
        settings.XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromHours(25));
    }

    [Fact]
    public async Task SetTextTime_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.XpTimeoutPeriod, 10,
            TestContext.Current.CancellationToken);

        var settings = await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        settings.XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task SetBase_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 20,
            TestContext.Current.CancellationToken);

        var settings = await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        settings.Base.Value.ShouldBe(20);
    }

    [Fact]
    public async Task SetModifier_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, 100,
            TestContext.Current.CancellationToken);

        var settings = await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        settings.Modifier.Value.ShouldBe(100);
    }

    [Fact]
    public async Task SetAmount_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 10,
            TestContext.Current.CancellationToken);

        var settings = await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        settings.Amount.Value.ShouldBe(10);
    }

    [Fact]
    public async Task TextTime_BelowRange_ReturnsInvalid()
    {
        var result =
            await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.XpTimeoutPeriod, 0,
                TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task TextTime_AboveRange_ReturnsInvalid()
    {
        var result =
            await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.XpTimeoutPeriod, 61,
                TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Amount_BelowRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 0,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Amount_AboveRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 101,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Base_AboveRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 501,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Modifier_AboveRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, 201,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task MultipleWrites_LatestWins()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 20,
            TestContext.Current.CancellationToken);
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 30,
            TestContext.Current.CancellationToken);

        // Fresh SUT to bypass the cache populated by the second write.
        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var settings = await freshSut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        settings.Base.Value.ShouldBe(30);
    }

    [Fact]
    public async Task RedundantWrite_ReturnsUnchanged_NoNewRow()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 20,
            TestContext.Current.CancellationToken);

        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 20,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.NotModified>();

        await using var db = factory.CreateDbContext();
        var count = await db.GuildSettings
            .Where(x => x.GuildId == _guildId && x.Type == GuildSettingType.LevelScalingBase)
            .CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task NewValue_ReturnsWritten_CacheInvalidated()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 20,
            TestContext.Current.CancellationToken);

        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 30,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Success>();

        var settings = await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();
        settings.Base.Value.ShouldBe(30);
    }

    [Fact]
    public async Task Base_MinExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 1,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed().Base
            .Value.ShouldBe(1);
    }

    [Fact]
    public async Task Base_MaxExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 500,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed().Base
            .Value.ShouldBe(500);
    }

    [Fact]
    public async Task Base_BelowMin_ReturnsInvalid()
        => (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 0,
                TestContext.Current.CancellationToken))
            .ShouldBeOfType<Result<int>.Invalid>();

    [Fact]
    public async Task Modifier_MinExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, 1,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed().Modifier
            .Value.ShouldBe(1);
    }

    [Fact]
    public async Task Modifier_MaxExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, 200,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed().Modifier
            .Value.ShouldBe(200);
    }

    [Fact]
    public async Task Modifier_BelowMin_ReturnsInvalid()
        => (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, 0,
                TestContext.Current.CancellationToken))
            .ShouldBeOfType<Result<int>.Invalid>();

    [Fact]
    public async Task Amount_MinExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 1,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed().Amount
            .Value.ShouldBe(1);
    }

    [Fact]
    public async Task Amount_MaxExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 100,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed().Amount
            .Value.ShouldBe(100);
    }

    [Fact]
    public async Task TextTime_MinExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.XpTimeoutPeriod, 1,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .XpTimeoutPeriod.Value
            .ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task TextTime_MaxExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.XpTimeoutPeriod, 60,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .XpTimeoutPeriod.Value
            .ShouldBe(TimeSpan.FromMinutes(60));
    }

    [Fact]
    public async Task CacheInvalidated_AfterSuccessfulWrite()
    {
        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed().Base
            .Value.ShouldBe(15);

        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 99,
            TestContext.Current.CancellationToken);

        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed().Base
            .Value.ShouldBe(99);
    }

    [Fact]
    public async Task TwoGuilds_IndependentSettings_NoInterference()
    {
        var guildB = new GuildId(2UL);

        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 100,
            TestContext.Current.CancellationToken);
        await this._sut.SetLevelingSettings(guildB, _modId, LevelSettings.Base, 200,
            TestContext.Current.CancellationToken);

        (await this._sut.GetLevelingSettings(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed().Base
            .Value.ShouldBe(100);
        (await this._sut.GetLevelingSettings(guildB, TestContext.Current.CancellationToken)).ShouldSucceed().Base.Value
            .ShouldBe(200);
    }

    // ── LevelingSettingEntry math ─────────────────────────────────────────────

    [Fact]
    public void GetLevelFromXp_Zero_ReturnsLevel1()
    {
        var entry = DefaultEntry();
        entry.GetLevelFromXp(0).ShouldBe(1);
    }

    [Fact]
    public void GetLevelFromXp_BelowLevel2Threshold_ReturnsLevel1()
    {
        var entry = DefaultEntry();
        entry.GetLevelFromXp(14).ShouldBe(1);
    }

    [Fact]
    public void GetLevelFromXp_AtLevel2Threshold_ReturnsLevel2()
    {
        var entry = DefaultEntry();
        entry.GetLevelFromXp(15).ShouldBe(2);
    }

    [Fact]
    public void GetLevelFromXp_HighXp_UsesOptimizationPath()
    {
        var entry = DefaultEntry();
        var level = entry.GetLevelFromXp(10000);
        level.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void GetLevelFromXp_ConsistentWithGetXpNeededForLevel()
    {
        var entry = DefaultEntry();
        for (var level = 1; level <= 10; level++)
        {
            var xpNeeded = entry.GetXpNeededForLevel(level);
            entry.GetLevelFromXp(xpNeeded).ShouldBe(level,
                $"xp {xpNeeded} (for level {level}) should yield level {level}");
        }
    }

    [Fact]
    public void GetXpNeededForLevel_Level1_ReturnsZero()
    {
        var entry = DefaultEntry();
        entry.GetXpNeededForLevel(1).ShouldBe(0);
    }

    [Fact]
    public void GetXpNeededForLevel_Level2_ReturnsBase()
    {
        var entry = DefaultEntry();
        entry.GetXpNeededForLevel(2).ShouldBe(15);
    }

    [Fact]
    public void GetXpNeededForLevel_Level3_GreaterThanLevel2()
    {
        var entry = DefaultEntry();
        entry.GetXpNeededForLevel(3).ShouldBeGreaterThan(entry.GetXpNeededForLevel(2));
    }

    [Fact]
    public void GetXpNeededForLevel_WithPositiveModifier_IncreasesXpNeeded()
    {
        var entry = DefaultEntry();
        entry.GetXpNeededForLevel(5).ShouldBeGreaterThan(entry.GetXpNeededForLevel(4));
    }

    [Fact]
    public void GetXpNeededForLevel_Negative_ReturnsZero()
    {
        var entry = DefaultEntry();
        entry.GetXpNeededForLevel(-5).ShouldBe(0);
    }

    [Fact]
    public void GetXpNeededForLevel_WithLevelModifier_AdjustsResult()
    {
        var entry = DefaultEntry();
        entry.GetXpNeededForLevel(3).ShouldBe(entry.GetXpNeededForLevel(2, 1));
    }

    private static LevelingSettingEntry DefaultEntry()
        => new(XpTimeoutPeriod.Default, LevelScalingModifier.Default, LevelScalingBase.Default, XpGainAmount.Default);
}

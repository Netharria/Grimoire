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

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task NoRows_ReturnsAllDefaults()
    {
        var settings = (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!);

        settings.XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromMinutes(3));
        settings.Base.Value.ShouldBe(15);
        settings.Modifier.Value.ShouldBe(50);
        settings.Amount.Value.ShouldBe(5);
    }

    [Fact]
    public async Task SetTextTime_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.XpTimeoutPeriod, 10);

        var settings = (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!);

        settings.XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task SetBase_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 20);

        var settings = (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!);

        settings.Base.Value.ShouldBe(20);
    }

    [Fact]
    public async Task SetModifier_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, 100);

        var settings = (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!);

        settings.Modifier.Value.ShouldBe(100);
    }

    [Fact]
    public async Task SetAmount_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 10);

        var settings = (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!);

        settings.Amount.Value.ShouldBe(10);
    }

    [Fact]
    public async Task TextTime_BelowRange_ReturnsInvalid()
    {
        var result =
            await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.XpTimeoutPeriod, 0);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task TextTime_AboveRange_ReturnsInvalid()
    {
        var result =
            await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.XpTimeoutPeriod, 61);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Amount_BelowRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 0);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Amount_AboveRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 101);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Base_AboveRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 501);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Modifier_AboveRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, 201);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task MultipleWrites_LatestWins()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 20);
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 30);

        // Fresh SUT to bypass the cache populated by the second write.
        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var settings = (await freshSut.GetLevelingSettings(_guildId)).OrElse(default!);

        settings.Base.Value.ShouldBe(30);
    }

    [Fact]
    public async Task RedundantWrite_ReturnsUnchanged_NoNewRow()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 20);

        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 20);

        result.ShouldBeOfType<Result<int>.NotModified>();

        await using var db = factory.CreateDbContext();
        var count = await db.GuildSettings
            .Where(x => x.GuildId == _guildId && x.Type == GuildSettingType.LevelScalingBase)
            .CountAsync();
        count.ShouldBe(1);
    }

    [Fact]
    public async Task NewValue_ReturnsWritten_CacheInvalidated()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 20);

        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 30);

        result.ShouldBeOfType<Result<int>.Success>();

        var settings = (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!);
        settings.Base.Value.ShouldBe(30);
    }

    [Fact]
    public async Task Base_MinExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 1);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).Base.Value.ShouldBe(1);
    }

    [Fact]
    public async Task Base_MaxExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 500);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).Base.Value.ShouldBe(500);
    }

    [Fact]
    public async Task Base_BelowMin_ReturnsInvalid()
        => (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 0))
            .ShouldBeOfType<Result<int>.Invalid>();

    [Fact]
    public async Task Modifier_MinExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, 1);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).Modifier.Value.ShouldBe(1);
    }

    [Fact]
    public async Task Modifier_MaxExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, 200);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).Modifier.Value.ShouldBe(200);
    }

    [Fact]
    public async Task Modifier_BelowMin_ReturnsInvalid()
        => (await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Modifier, 0))
            .ShouldBeOfType<Result<int>.Invalid>();

    [Fact]
    public async Task Amount_MinExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 1);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).Amount.Value.ShouldBe(1);
    }

    [Fact]
    public async Task Amount_MaxExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Amount, 100);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).Amount.Value.ShouldBe(100);
    }

    [Fact]
    public async Task TextTime_MinExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.XpTimeoutPeriod, 1);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task TextTime_MaxExact_IsValid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.XpTimeoutPeriod, 60);

        result.ShouldBeOfType<Result<int>.Success>();
        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromMinutes(60));
    }

    [Fact]
    public async Task CacheInvalidated_AfterSuccessfulWrite()
    {
        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).Base.Value.ShouldBe(15);

        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 99);

        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).Base.Value.ShouldBe(99);
    }

    [Fact]
    public async Task TwoGuilds_IndependentSettings_NoInterference()
    {
        var guildB = new GuildId(2UL);

        await this._sut.SetLevelingSettings(_guildId, _modId, LevelSettings.Base, 100);
        await this._sut.SetLevelingSettings(guildB, _modId, LevelSettings.Base, 200);

        (await this._sut.GetLevelingSettings(_guildId)).OrElse(default!).Base.Value.ShouldBe(100);
        (await this._sut.GetLevelingSettings(guildB)).OrElse(default!).Base.Value.ShouldBe(200);
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
        entry.GetXpNeededForLevel(3, 0).ShouldBe(entry.GetXpNeededForLevel(2, 1));
    }

    private static SettingsModule.LevelingSettingEntry DefaultEntry()
        => new(
            XpTimeoutPeriod.FromDatabaseOrDefault(null),
            LevelScalingModifier.FromDatabaseOrDefault(null),
            LevelScalingBase.FromDatabaseOrDefault(null),
            XpGainAmount.FromDatabaseOrDefault(null));
}

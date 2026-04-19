// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

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
        var settings = await this._sut.GetLevelingSettings(_guildId);

        settings.XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromMinutes(3));
        settings.Base.Value.ShouldBe(15);
        settings.Modifier.Value.ShouldBe(50);
        settings.Amount.Value.ShouldBe(5);
    }

    [Fact]
    public async Task SetTextTime_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.XpTimeoutPeriod, 10);

        var settings = await this._sut.GetLevelingSettings(_guildId);

        settings.XpTimeoutPeriod.Value.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task SetBase_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Base, 20);

        var settings = await this._sut.GetLevelingSettings(_guildId);

        settings.Base.Value.ShouldBe(20);
    }

    [Fact]
    public async Task SetModifier_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Modifier, 100);

        var settings = await this._sut.GetLevelingSettings(_guildId);

        settings.Modifier.Value.ShouldBe(100);
    }

    [Fact]
    public async Task SetAmount_RoundTrips()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Amount, 10);

        var settings = await this._sut.GetLevelingSettings(_guildId);

        settings.Amount.Value.ShouldBe(10);
    }

    [Fact]
    public async Task TextTime_BelowRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.XpTimeoutPeriod, 0);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task TextTime_AboveRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.XpTimeoutPeriod, 61);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Amount_BelowRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Amount, 0);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Amount_AboveRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Amount, 101);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Base_AboveRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Base, 501);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task Modifier_AboveRange_ReturnsInvalid()
    {
        var result = await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Modifier, 201);

        result.ShouldBeOfType<Result<int>.Invalid>();
    }

    [Fact]
    public async Task MultipleWrites_LatestWins()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Base, 20);
        await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Base, 30);

        // Fresh SUT to bypass the cache populated by the second write.
        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var settings = await freshSut.GetLevelingSettings(_guildId);

        settings.Base.Value.ShouldBe(30);
    }

    [Fact]
    public async Task RedundantWrite_ReturnsUnchanged_NoNewRow()
    {
        await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Base, 20);

        var result = await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Base, 20);

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
        await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Base, 20);

        var result = await this._sut.SetLevelingSettings(_guildId, _modId, SettingsModule.LevelSettings.Base, 30);

        result.ShouldBeOfType<Result<int>.Success>();

        var settings = await this._sut.GetLevelingSettings(_guildId);
        settings.Base.Value.ShouldBe(30);
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.IntegrationTests.Database.SettingsLayer.ModuleState;

[Collection("Settings collection")]
public sealed class SetModuleStateTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    [Fact]
    public async Task EnableModule_WritesCustomValueTrueRow()
    {
        var result = await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true,
            TestContext.Current.CancellationToken);

        result.ShouldSucceed();
        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeTrue();
    }

    [Fact]
    public async Task DisableModule_WritesDisabledRow()
    {
        var result = await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, false,
            TestContext.Current.CancellationToken);

        result.ShouldSucceed();
        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task NoRowInDb_IsModuleEnabled_ReturnsFalse()
    {
        foreach (var module in new[]
                 {
                     Module.Leveling, Module.UserLog, Module.Moderation, Module.MessageLog, Module.Commands,
                     Module.AntiSpam
                 })
            (await this._sut.IsModuleEnabled(module, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
                .ShouldBeFalse();
    }

    [Fact]
    public async Task GeneralModule_AlwaysEnabled()
    {
        (await this._sut.IsModuleEnabled(Module.General, _guildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeTrue();

        var result = await this._sut.SetModuleState(Module.General, _guildId, _modId, true,
            TestContext.Current.CancellationToken);

        result.ShouldFail().ShouldBeOfType<Invalid>();
    }

    [Fact]
    public async Task RedundantEnable_ReturnsUnchanged_NoNewRow()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true, TestContext.Current.CancellationToken);

        var result = await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true,
            TestContext.Current.CancellationToken);

        result.ShouldBeUnchanged();

        await using var db = factory.CreateDbContext();
        var count = await db.GuildSettings
            .Where(x => x.GuildId == _guildId && x.Type == GuildSettingType.LevelingModuleEnabled)
            .CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task RedundantDisable_ReturnsUnchanged_NoNewRow()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, false, TestContext.Current.CancellationToken);

        var result = await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, false,
            TestContext.Current.CancellationToken);

        result.ShouldBeUnchanged();

        await using var db = factory.CreateDbContext();
        var count = await db.GuildSettings
            .Where(x => x.GuildId == _guildId && x.Type == GuildSettingType.LevelingModuleEnabled)
            .CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAllModuleState_ReflectsAllSixModules()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true, TestContext.Current.CancellationToken);
        await this._sut.SetModuleState(Module.UserLog, _guildId, _modId, true, TestContext.Current.CancellationToken);
        await this._sut.SetModuleState(Module.MessageLog, _guildId, _modId, true,
            TestContext.Current.CancellationToken);
        await this._sut.SetModuleState(Module.Moderation, _guildId, _modId, false,
            TestContext.Current.CancellationToken);
        await this._sut.SetModuleState(Module.Commands, _guildId, _modId, false, TestContext.Current.CancellationToken);

        var state = await this._sut.GetAllModuleState(_guildId, TestContext.Current.CancellationToken).ShouldSucceed();

        state.LevelingEnabled.ShouldBeTrue();
        state.UserLogEnabled.ShouldBeTrue();
        state.MessageLogEnabled.ShouldBeTrue();
        state.ModerationEnabled.ShouldBeFalse();
        state.CommandsEnabled.ShouldBeFalse();
        state.AntiSpamEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task GeneralModule_SetState_HasCorrectErrorCode()
    {
        var result = await this._sut.SetModuleState(Module.General, _guildId, _modId, true,
            TestContext.Current.CancellationToken);

        var invalid = result.ShouldFail().ShouldBeOfType<Invalid>();
        invalid.Message.ShouldBe("Cannot disable the general module.");
    }

    [Fact]
    public async Task TwoGuilds_ModuleState_Independent()
    {
        var guildB = new GuildId(2UL);

        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true, TestContext.Current.CancellationToken);
        await this._sut.SetModuleState(Module.Leveling, guildB, _modId, false, TestContext.Current.CancellationToken);

        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeTrue();
        (await this._sut.IsModuleEnabled(Module.Leveling, guildB, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task GetGuildSetting_MultipleRows_LatestWins()
    {
        await using var db = factory.CreateDbContext();
        db.GuildSettings.Add(
            GuildSettingCustomValue.Create(
                GuildSettingType.LevelingModuleEnabled, _guildId, _modId, DateTimeOffset.UtcNow.AddHours(-2),
                bool.TrueString).ShouldSucceed());
        db.GuildSettings.Add(
            GuildSettingDisabled.Create(
                    GuildSettingType.LevelingModuleEnabled, _guildId, _modId, DateTimeOffset.UtcNow.AddHours(-1))
                );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task SetGuildSetting_MultipleRows_LatestDeterminesRedundancy()
    {
        await using var db = factory.CreateDbContext();
        db.GuildSettings.Add(
            GuildSettingCustomValue.Create(
                GuildSettingType.LevelingModuleEnabled, _guildId, _modId, DateTimeOffset.UtcNow.AddHours(-2),
                bool.TrueString).ShouldSucceed());
        db.GuildSettings.Add(
            GuildSettingDisabled.Create(
                    GuildSettingType.LevelingModuleEnabled, _guildId, _modId, DateTimeOffset.UtcNow.AddHours(-1))
                );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, false,
            TestContext.Current.CancellationToken);

        result.ShouldBeUnchanged();
    }

    [Fact]
    public async Task RedundantEnable_NotModified_HasCorrectErrorCode()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true, TestContext.Current.CancellationToken);

        var result = await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true,
            TestContext.Current.CancellationToken);

        result.ShouldBeUnchanged().ShouldBe("The setting was already set to that value.");
    }

    [Fact]
    public async Task CacheCleared_AfterModuleStateChange()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true, TestContext.Current.CancellationToken);
        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeTrue();

        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, false, TestContext.Current.CancellationToken);

        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeFalse();
    }

    /// <remarks>Cases: XC-90.</remarks>
    [Fact]
    public async Task CacheInvalidatedAfterStateChange()
    {
        await using (var db = factory.CreateDbContext())
        {
            db.GuildSettings.Add(
                GuildSettingCustomValue.Create(
                    GuildSettingType.LevelingModuleEnabled, _guildId, _modId, DateTimeOffset.UtcNow.AddHours(-2),
                    bool.TrueString).ShouldSucceed());
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeTrue();

        await using (var db = factory.CreateDbContext())
        {
            db.GuildSettings.Add(
                GuildSettingDisabled.Create(
                        GuildSettingType.LevelingModuleEnabled, _guildId, _modId, DateTimeOffset.UtcNow.AddHours(-1))
                    );
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeTrue();

        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true, TestContext.Current.CancellationToken);

        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeTrue();
    }
}

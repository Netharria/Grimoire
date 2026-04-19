// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.ModuleState;

[Collection("Settings collection")]
public sealed class SetModuleStateTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task EnableModule_WritesCustomValueTrueRow()
    {
        var result = await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);

        result.ShouldBeOfType<Result<GuildSetting>.Success>();
        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId)).ShouldBeTrue();
    }

    [Fact]
    public async Task DisableModule_WritesDisabledRow()
    {
        var result = await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, false);

        result.ShouldBeOfType<Result<GuildSetting>.Success>();
        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId)).ShouldBeFalse();
    }

    [Fact]
    public async Task NoRowInDb_IsModuleEnabled_ReturnsFalse()
    {
        foreach (var module in new[]
                 {
                     Module.Leveling, Module.UserLog, Module.Moderation, Module.MessageLog, Module.Commands,
                     Module.AntiSpam
                 })
            (await this._sut.IsModuleEnabled(module, _guildId)).ShouldBeFalse();
    }

    [Fact]
    public async Task GeneralModule_AlwaysEnabled()
    {
        (await this._sut.IsModuleEnabled(Module.General, _guildId)).ShouldBeTrue();

        var result = await this._sut.SetModuleState(Module.General, _guildId, _modId, true);

        result.ShouldBeOfType<Result<GuildSetting>.Invalid>();
    }

    [Fact]
    public async Task RedundantEnable_ReturnsUnchanged_NoNewRow()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);

        var result = await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);

        result.ShouldBeOfType<Result<GuildSetting>.NotModified>();

        await using var db = factory.CreateDbContext();
        var count = await db.GuildSettings
            .Where(x => x.GuildId == _guildId && x.Type == GuildSettingType.LevelingModuleEnabled)
            .CountAsync();
        count.ShouldBe(1);
    }

    [Fact]
    public async Task RedundantDisable_ReturnsUnchanged_NoNewRow()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, false);

        var result = await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, false);

        result.ShouldBeOfType<Result<GuildSetting>.NotModified>();

        await using var db = factory.CreateDbContext();
        var count = await db.GuildSettings
            .Where(x => x.GuildId == _guildId && x.Type == GuildSettingType.LevelingModuleEnabled)
            .CountAsync();
        count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAllModuleState_ReflectsAllSixModules()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);
        await this._sut.SetModuleState(Module.UserLog, _guildId, _modId, true);
        await this._sut.SetModuleState(Module.MessageLog, _guildId, _modId, true);
        await this._sut.SetModuleState(Module.Moderation, _guildId, _modId, false);
        await this._sut.SetModuleState(Module.Commands, _guildId, _modId, false);

        var state = await this._sut.GetAllModuleState(_guildId);

        state.LevelingEnabled.ShouldBeTrue();
        state.UserLogEnabled.ShouldBeTrue();
        state.MessageLogEnabled.ShouldBeTrue();
        state.ModerationEnabled.ShouldBeFalse();
        state.CommandsEnabled.ShouldBeFalse();
        state.AntiSpamEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task CacheInvalidatedAfterStateChange()
    {
        await using (var db = factory.CreateDbContext())
        {
            db.GuildSettings.Add(new GuildSettingCustomValue(
                GuildSettingType.LevelingModuleEnabled, _guildId, _modId, DateTimeOffset.UtcNow.AddHours(-2),
                bool.TrueString));
            await db.SaveChangesAsync();
        }

        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId)).ShouldBeTrue();

        await using (var db = factory.CreateDbContext())
        {
            db.GuildSettings.Add(new GuildSettingDisabled(
                GuildSettingType.LevelingModuleEnabled, _guildId, _modId, DateTimeOffset.UtcNow.AddHours(-1)));
            await db.SaveChangesAsync();
        }

        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId)).ShouldBeTrue();

        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);

        (await this._sut.IsModuleEnabled(Module.Leveling, _guildId)).ShouldBeTrue();
    }
}

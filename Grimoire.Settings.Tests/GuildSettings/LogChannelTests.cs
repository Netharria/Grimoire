// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.GuildSettings;

[Collection("Settings collection")]
public sealed class LogChannelTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly ChannelId _channelId = new(200UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task ModuleEnabled_ChannelSet_GetEffectiveReturnsChannel()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);
        await this._sut.SetLogChannelSetting(GuildLogType.Leveling, _guildId, _modId, _channelId);

        var result = await this._sut.GetLogChannelSetting(GuildLogType.Leveling, _guildId);

        result.ShouldSucceed().ShouldBe(_channelId);
    }

    [Fact]
    public async Task ModuleEnabled_NoChannel_GetEffectiveReturnsNull()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);

        var result = await this._sut.GetLogChannelSetting(GuildLogType.Leveling, _guildId);

        result.ShouldSucceed().ShouldBeNull();
    }

    [Fact]
    public async Task SetNull_WritesDisabled_GetConfiguredReturnsNull()
    {
        await this._sut.SetLogChannelSetting(GuildLogType.Leveling, _guildId, _modId, _channelId);
        await this._sut.SetLogChannelSetting(GuildLogType.Leveling, _guildId, _modId, null);

        var result = await this._sut.GetLogChannelSetting(GuildLogType.Leveling, _guildId);

        result.ShouldSucceed().ShouldBeNull();
    }

    [Fact]
    public void AllGuildLogTypes_MapWithoutException()
    {
        foreach (var logType in Enum.GetValues<GuildLogType>())
        {
            var act1 = () => logType.ToGuildSettingType();
            var act2 = () => logType.GetLogTypeModule();
            act1.ShouldNotThrow();
            act2.ShouldNotThrow();
        }
    }
}

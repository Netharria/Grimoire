// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.GuildSettings;

[Collection("Settings collection")]
public sealed class UserCommandChannelTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly ChannelId _channelId = new(200UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task SetChannel_StoresValue_GetReturnsIt()
    {
        await this._sut.SetUserCommandChannelSetting(_guildId, _modId, _channelId);

        var result = await this._sut.GetUserCommandChannel(_guildId);

        result.ShouldBe(_channelId);
    }

    [Fact]
    public async Task SetNull_WritesDisabled_GetReturnsNull()
    {
        await this._sut.SetUserCommandChannelSetting(_guildId, _modId, _channelId);
        await this._sut.SetUserCommandChannelSetting(_guildId, _modId, null);

        var result = await this._sut.GetUserCommandChannel(_guildId);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RedundantWrite_ReturnsNotModified()
    {
        await this._sut.SetUserCommandChannelSetting(_guildId, _modId, _channelId);

        var result = await this._sut.SetUserCommandChannelSetting(_guildId, _modId, _channelId);

        result.ShouldBeOfType<Result<ChannelId?>.NotModified>();
    }

    [Fact]
    public async Task NoRow_GetReturnsNull()
    {
        var result = await this._sut.GetUserCommandChannel(_guildId);

        result.ShouldBeNull();
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.Moderation;

[Collection("Settings collection")]
public sealed class SpamFilterOverrideTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly ChannelId _channelId = new(200UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task NoRow_ReturnsInherit()
    {
        var result = await this._sut.GetSpamFilterOverrideAsync(_guildId, _channelId);

        result.ShouldBe(SpamFilterOverrideOption.Inherit);
    }

    [Fact]
    public async Task AlwaysFilter_ReturnsAlwaysFilter()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter);

        var result = await this._sut.GetSpamFilterOverrideAsync(_guildId, _channelId);

        result.ShouldBe(SpamFilterOverrideOption.AlwaysFilter);
    }

    [Fact]
    public async Task NeverFilter_ReturnsNeverFilter()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.NeverFilter);

        var result = await this._sut.GetSpamFilterOverrideAsync(_guildId, _channelId);

        result.ShouldBe(SpamFilterOverrideOption.NeverFilter);
    }

    [Fact]
    public async Task MultipleHistoricalRows_LatestWins()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter);
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.NeverFilter);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetSpamFilterOverrideAsync(_guildId, _channelId);

        result.ShouldBe(SpamFilterOverrideOption.NeverFilter);
    }

    [Fact]
    public async Task RedundantWrite_ReturnsNotModified_NoNewRow()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter);

        var result =
            await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId,
                SpamFilterOverrideOption.AlwaysFilter);

        result.ShouldBeOfType<Result<SpamFilterOverride>.NotModified>();

        await using var db = factory.CreateDbContext();
        var count = await db.SpamFilterOverrides
            .Where(x => x.ChannelId == _channelId && x.GuildId == _guildId)
            .CountAsync();
        count.ShouldBe(1);
    }

    [Fact]
    public async Task NewValue_InsertsRow_CacheUpdated()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter);

        var result =
            await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId,
                SpamFilterOverrideOption.NeverFilter);

        result.ShouldBeOfType<Result<SpamFilterOverride>.Success>();

        (await this._sut.GetSpamFilterOverrideAsync(_guildId, _channelId)).ShouldBe(
            SpamFilterOverrideOption.NeverFilter);
    }

    [Fact]
    public async Task GetAll_ExcludesInherit()
    {
        var inheritChannel = new ChannelId(201UL);
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter);
        await this._sut.SetSpamFilterOverrideAsync(inheritChannel, _guildId, _modId, SpamFilterOverrideOption.Inherit);

        var overrides = await this._sut.GetAllSpamFilterOverrideAsync(_guildId).ToListAsync();

        overrides.Count.ShouldBe(1);
        overrides.Single().ChannelId.ShouldBe(_channelId);
    }

    [Fact]
    public async Task GetAll_EmptyGuild_ReturnsEmpty()
    {
        var overrides = await this._sut.GetAllSpamFilterOverrideAsync(_guildId).ToListAsync();

        overrides.ShouldBeEmpty();
    }
}

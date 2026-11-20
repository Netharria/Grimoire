// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Documents;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Grimoire.IntegrationTests.Database.SettingsLayer.Moderation;

[Collection("Settings collection")]
public sealed class SpamFilterOverrideTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly ChannelId _channelId = new(200UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    [Fact]
    public async Task NoOverride_ReturnsNone()
    {
        var result = await this._sut
            .GetSpamFilterOverrideAsync(_guildId, _channelId, TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBe(Option<SpamFilterOverrideOption>.None);
    }

    [Fact]
    public async Task AlwaysFilter_ReturnsAlwaysFilter()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter,
            TestContext.Current.CancellationToken);

        var result = await this._sut
            .GetSpamFilterOverrideAsync(_guildId, _channelId, TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBe(Some(SpamFilterOverrideOption.AlwaysFilter));
    }

    [Fact]
    public async Task NeverFilter_ReturnsNeverFilter()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.NeverFilter,
            TestContext.Current.CancellationToken);

        var result = await this._sut
            .GetSpamFilterOverrideAsync(_guildId, _channelId, TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBe(Some(SpamFilterOverrideOption.NeverFilter));
    }

    [Fact]
    public async Task AChangedOverride_ReadsTheNewValue()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter,
            TestContext.Current.CancellationToken);
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.NeverFilter,
            TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut
            .GetSpamFilterOverrideAsync(_guildId, _channelId, TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBe(Some(SpamFilterOverrideOption.NeverFilter));
    }

    [Fact]
    public async Task RedundantWrite_ReturnsUnchanged_NoNewVersion()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter,
            TestContext.Current.CancellationToken);

        var result =
            await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId,
                SpamFilterOverrideOption.AlwaysFilter, TestContext.Current.CancellationToken);

        result.ShouldBeUnchanged();

        await using var db = factory.CreateDbContext();
        (await SettingsRows.LatestDocument(db, _guildId, SettingsDocument.SpamFilterOverrides,
            TestContext.Current.CancellationToken))!.Version.ShouldBe(1);
    }

    /// <remarks>Cases: XC-96.</remarks>
    [Fact]
    public async Task NewValue_IsSaved_CacheCleared()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter,
            TestContext.Current.CancellationToken);

        var result =
            await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId,
                SpamFilterOverrideOption.NeverFilter, TestContext.Current.CancellationToken);

        result.ShouldSucceed();

        (await this._sut.GetSpamFilterOverrideAsync(_guildId, _channelId, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBe(Some(SpamFilterOverrideOption.NeverFilter));
    }

    [Fact]
    public async Task GetAll_LeavesOutAClearedOverride()
    {
        var inheritChannel = new ChannelId(201UL);
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter,
            TestContext.Current.CancellationToken);
        await this._sut.SetSpamFilterOverrideAsync(inheritChannel, _guildId, _modId, SpamFilterOverrideOption.NeverFilter,
            TestContext.Current.CancellationToken);
        await this._sut.SetSpamFilterOverrideAsync(inheritChannel, _guildId, _modId, None,
            TestContext.Current.CancellationToken);

        var overrides = await this._sut.GetAllSpamFilterOverrideAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        overrides.Count.ShouldBe(1);
        overrides.Keys.Single().ShouldBe(_channelId);
    }

    [Fact]
    public async Task GetAll_EmptyGuild_ReturnsEmpty()
    {
        var overrides = await this._sut.GetAllSpamFilterOverrideAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        overrides.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task SetSpamFilterOverride_NotModified_HasCorrectErrorCode()
    {
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter,
            TestContext.Current.CancellationToken);

        var result = await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId,
            SpamFilterOverrideOption.AlwaysFilter, TestContext.Current.CancellationToken);

        result.ShouldBeUnchanged().ShouldBe("The channel is already set to this spam filter option.");
    }

    [Fact]
    public async Task CacheKey_TwoDifferentChannels_NoCacheInterference()
    {
        var channelB = new ChannelId(201UL);
        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter,
            TestContext.Current.CancellationToken);

        var resultB = await this._sut
            .GetSpamFilterOverrideAsync(_guildId, channelB, TestContext.Current.CancellationToken).ShouldSucceed();

        resultB.ShouldBe(Option<SpamFilterOverrideOption>.None);
    }

    [Fact]
    public async Task GetSpamFilterOverride_SameChannelDifferentGuild_ReturnsNone()
    {
        var guildB = new GuildId(2UL);

        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter,
            TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut
            .GetSpamFilterOverrideAsync(guildB, _channelId, TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBe(Option<SpamFilterOverrideOption>.None);
    }

    [Fact]
    public async Task GetAll_ExcludesOtherGuild()
    {
        var guildB = new GuildId(2UL);
        var channelB = new ChannelId(201UL);

        await this._sut.SetSpamFilterOverrideAsync(_channelId, _guildId, _modId, SpamFilterOverrideOption.AlwaysFilter,
            TestContext.Current.CancellationToken);
        await this._sut.SetSpamFilterOverrideAsync(channelB, guildB, _modId, SpamFilterOverrideOption.NeverFilter,
            TestContext.Current.CancellationToken);

        var overrides = await this._sut.GetAllSpamFilterOverrideAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        overrides.Count.ShouldBe(1);
        overrides.Keys.Single().ShouldBe(_channelId);
    }
}

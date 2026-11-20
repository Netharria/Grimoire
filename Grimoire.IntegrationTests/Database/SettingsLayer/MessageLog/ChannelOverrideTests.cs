// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Documents;
using static LanguageExt.Prelude;

namespace Grimoire.IntegrationTests.Database.SettingsLayer.MessageLog;

[Collection("Settings collection")]
public sealed class ChannelOverrideTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly ChannelId _channelId = new(200UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public async ValueTask InitializeAsync()
        => await this._sut.SetModuleState(Module.MessageLog, _guildId, _modId, true);

    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    [Fact]
    public async Task ModuleDisabled_ShouldLogMessage_ReturnsFalse()
    {
        await this._sut.SetModuleState(Module.MessageLog, _guildId, _modId, false,
            TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AlwaysLog_Override_ReturnsTrue()
    {
        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);

        var result = await this._sut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task NeverLog_Override_ReturnsFalse()
    {
        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.NeverLog,
            TestContext.Current.CancellationToken);

        var result = await this._sut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Inherit_ParentAlwaysLog_ReturnsTrue()
    {
        var parentChannelId = new ChannelId(201UL);
        await this._sut.SetChannelLogOverride(parentChannelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);

        var channelNodes = new Dictionary<ChannelId, ChannelId?> { [_channelId] = parentChannelId };
        var result = await this._sut
            .ShouldLogMessage(_channelId, _guildId, channelNodes, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeTrue();
    }

    /// <remarks>Cases: LOG-24.</remarks>
    [Fact]
    public async Task Inherit_ParentNeverLog_ReturnsFalse()
    {
        var parentChannelId = new ChannelId(201UL);
        await this._sut.SetChannelLogOverride(parentChannelId, _guildId, _modId, MessageLogOverrideOption.NeverLog,
            TestContext.Current.CancellationToken);

        var channelNodes = new Dictionary<ChannelId, ChannelId?> { [_channelId] = parentChannelId };
        var result = await this._sut
            .ShouldLogMessage(_channelId, _guildId, channelNodes, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Inherit_NoParent_ReturnsTrue()
    {
        var result = await this._sut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeTrue();
    }

    /// <remarks>Cases: LOG-24.</remarks>
    [Fact]
    public async Task DeepHierarchy_Inherit_Inherit_AlwaysLog()
    {
        var mid = new ChannelId(201UL);
        var root = new ChannelId(202UL);
        await this._sut.SetChannelLogOverride(root, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);

        var channelNodes = new Dictionary<ChannelId, ChannelId?> { [_channelId] = mid, [mid] = root };
        var result = await this._sut
            .ShouldLogMessage(_channelId, _guildId, channelNodes, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeTrue();
    }

    /// <remarks>Cases: LOG-26.</remarks>
    [Fact]
    public async Task RedundantWrite_ReturnsUnchanged_NoNewVersion()
    {
        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);

        var result =
            await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
                TestContext.Current.CancellationToken);

        result.ShouldBeUnchanged();

        await using var db = factory.CreateDbContext();
        (await SettingsRows.LatestDocument(db, _guildId, SettingsDocument.MessageLogOverrides,
            TestContext.Current.CancellationToken))!.Version.ShouldBe(1);
    }

    [Fact]
    public async Task NewValue_IsSaved_CacheCleared()
    {
        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);

        var result =
            await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.NeverLog,
                TestContext.Current.CancellationToken);

        result.ShouldSucceed();

        var shouldLog = await this._sut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken).ShouldSucceed();
        shouldLog.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllOverriddenChannels_LeavesOutAClearedOverride()
    {
        var neverChannel = new ChannelId(201UL);
        var inheritChannel = new ChannelId(202UL);

        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);
        await this._sut.SetChannelLogOverride(neverChannel, _guildId, _modId, MessageLogOverrideOption.NeverLog,
            TestContext.Current.CancellationToken);
        await this._sut.SetChannelLogOverride(inheritChannel, _guildId, _modId, MessageLogOverrideOption.NeverLog,
            TestContext.Current.CancellationToken);
        await this._sut.SetChannelLogOverride(inheritChannel, _guildId, _modId, None,
            TestContext.Current.CancellationToken);

        var overrides = await this._sut.GetAllOverriddenChannels(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        overrides.Keys.ShouldBe([_channelId, neverChannel]);
    }

    /// <remarks>Cases: LOG-26.</remarks>
    [Fact]
    public async Task SetChannelLogOverride_NotModified_HasCorrectErrorCode()
    {
        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);

        var result =
            await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
                TestContext.Current.CancellationToken);

        result.ShouldBeUnchanged().ShouldBe("The channel is already set to this log option.");
    }

    [Fact]
    public async Task CacheKey_TwoDifferentChannels_NoCacheInterference()
    {
        var channelB = new ChannelId(201UL);
        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.NeverLog,
            TestContext.Current.CancellationToken);

        var resultB = await this._sut.ShouldLogMessage(channelB, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken).ShouldSucceed();

        resultB.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldLogMessage_SameChannelDifferentGuild_DoesNotAffectResult()
    {
        var guildB = new GuildId(2UL);
        await this._sut.SetModuleState(Module.MessageLog, guildB, _modId, true, TestContext.Current.CancellationToken);
        await this._sut.SetChannelLogOverride(_channelId, guildB, _modId, MessageLogOverrideOption.NeverLog,
            TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeTrue();
    }

    /// <remarks>Cases: LOG-33.</remarks>
    [Fact]
    public async Task GetAllOverriddenChannels_ExcludesOtherGuild()
    {
        var guildB = new GuildId(2UL);
        var channelB = new ChannelId(201UL);

        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);
        await this._sut.SetChannelLogOverride(channelB, guildB, _modId, MessageLogOverrideOption.NeverLog,
            TestContext.Current.CancellationToken);

        var overrides = await this._sut.GetAllOverriddenChannels(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        overrides.Count.ShouldBe(1);
        overrides.Keys.Single().ShouldBe(_channelId);
    }

    /// <summary>
    ///     A version written straight to the database (as an edit by hand would be) is what a fresh module reads: the
    ///     latest version decides, not the newest row per channel (spec 2.5).
    /// </summary>
    [Fact]
    public async Task TheLatestVersion_Decides()
    {
        await using (var db = factory.CreateDbContext())
        {
            await SettingsRows.AddNextVersion(db, _guildId,
                MessageLogOverrides.Empty.With(_channelId, MessageLogOverrideOption.AlwaysLog),
                TestContext.Current.CancellationToken);
            await SettingsRows.AddNextVersion(db, _guildId,
                MessageLogOverrides.Empty.With(_channelId, MessageLogOverrideOption.NeverLog),
                TestContext.Current.CancellationToken);
        }

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeFalse();
    }

    /// <summary>KI-103 (spec 2.5): an unchanged write still clears the cache, so a direct edit becomes visible.</summary>
    [Fact]
    public async Task AnUnchangedWrite_StillClearsTheCache()
    {
        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);
        (await this._sut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeTrue();
        await using (var db = factory.CreateDbContext())
            await SettingsRows.AddNextVersion(db, _guildId,
                MessageLogOverrides.Empty.With(_channelId, MessageLogOverrideOption.NeverLog),
                TestContext.Current.CancellationToken);

        (await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.NeverLog,
            TestContext.Current.CancellationToken)).ShouldBeUnchanged();

        (await this._sut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeFalse();
    }
}

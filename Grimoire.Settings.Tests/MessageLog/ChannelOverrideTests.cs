// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.MessageLog;

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

    [Fact]
    public async Task RedundantWrite_ReturnsNotModified_NoNewRow()
    {
        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);

        var result =
            await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
                TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<MessageLogChannelOverride>.NotModified>();

        await using var db = factory.CreateDbContext();
        var count = await db.MessageLogChannelOverrides
            .Where(x => x.ChannelId == _channelId && x.GuildId == _guildId)
            .CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task NewValue_InsertsRow_CacheUpdated()
    {
        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);

        var result =
            await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.NeverLog,
                TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<MessageLogChannelOverride>.Success>();

        var shouldLog = await this._sut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken).ShouldSucceed();
        shouldLog.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllOverriddenChannels_ExcludesInherit()
    {
        var neverChannel = new ChannelId(201UL);
        var inheritChannel = new ChannelId(202UL);

        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);
        await this._sut.SetChannelLogOverride(neverChannel, _guildId, _modId, MessageLogOverrideOption.NeverLog,
            TestContext.Current.CancellationToken);
        await this._sut.SetChannelLogOverride(inheritChannel, _guildId, _modId, MessageLogOverrideOption.Inherit,
            TestContext.Current.CancellationToken);

        var overrides = await this._sut.GetAllOverriddenChannels(_guildId, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        overrides.Count.ShouldBe(2);
        overrides.ShouldContain(x => x.ChannelId == _channelId);
        overrides.ShouldContain(x => x.ChannelId == neverChannel);
        overrides.ShouldNotContain(x => x.ChannelId == inheritChannel);
    }

    [Fact]
    public async Task SetChannelLogOverride_NotModified_HasCorrectErrorCode()
    {
        await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
            TestContext.Current.CancellationToken);

        var result =
            await this._sut.SetChannelLogOverride(_channelId, _guildId, _modId, MessageLogOverrideOption.AlwaysLog,
                TestContext.Current.CancellationToken);

        var notModified = result.ShouldBeOfType<Result<MessageLogChannelOverride>.NotModified>();
        notModified.Error.Code.ShouldBe("channel-log-override.not-changed");
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
            .ToListAsync(TestContext.Current.CancellationToken);

        overrides.Count.ShouldBe(1);
        overrides.Single().ChannelId.ShouldBe(_channelId);
    }

    [Fact]
    public async Task SetChannelLogOverride_MultipleHistoricalRows_LatestWins()
    {
        await using (var db = factory.CreateDbContext())
        {
            var t1 = DateTimeOffset.UtcNow.AddHours(-2);
            var t2 = DateTimeOffset.UtcNow.AddHours(-1);
            db.MessageLogChannelOverrides.Add(
                ((Validation<MessageLogChannelOverride>.Valid)MessageLogChannelOverride.Create(
                    MessageLogOverrideOption.AlwaysLog, _channelId, _guildId, _modId, t1)).Value);
            db.MessageLogChannelOverrides.Add(
                ((Validation<MessageLogChannelOverride>.Valid)MessageLogChannelOverride.Create(
                    MessageLogOverrideOption.NeverLog, _channelId, _guildId, _modId, t2)).Value);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.ShouldLogMessage(_channelId, _guildId, new Dictionary<ChannelId, ChannelId?>(),
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeFalse();
    }
}

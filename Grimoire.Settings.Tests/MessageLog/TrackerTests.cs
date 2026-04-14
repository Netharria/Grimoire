// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.MessageLog;

[Collection("Settings collection")]
public sealed class TrackerTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly UserId _userId = new(100UL);
    private static readonly ChannelId _channelId = new(200UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task NoTracker_GetTrackerChannel_ReturnsNull()
    {
        var result = await this._sut.GetTrackerChannelAsync(_userId, _guildId);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task AddTracker_GetTrackerChannel_ReturnsChannel()
    {
        await this._sut.AddTracker(_userId, _modId, _guildId, _channelId, TimeSpan.FromDays(1));

        var result = await this._sut.GetTrackerChannelAsync(_userId, _guildId);

        result.ShouldBe(_channelId);
    }

    [Fact]
    public async Task AddTracker_ExistingTracker_UpdatesInPlace()
    {
        var newChannel = new ChannelId(201UL);
        await this._sut.AddTracker(_userId, _modId, _guildId, _channelId, TimeSpan.FromDays(1));
        await this._sut.AddTracker(_userId, _modId, _guildId, newChannel, TimeSpan.FromDays(2));

        await using var db = factory.CreateDbContext();
        var count = await db.Trackers.Where(x => x.UserId == _userId && x.GuildId == _guildId).CountAsync();
        count.ShouldBe(1);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.GetTrackerChannelAsync(_userId, _guildId)).ShouldBe(newChannel);
    }

    [Fact]
    public async Task RemoveTracker_NotTracked_ReturnsUnchanged()
    {
        var result = await this._sut.RemoveTracker(_userId, _guildId);

        result.ShouldBeOfType<SettingsUnchanged<Tracker?>>();
        ((SettingsUnchanged<Tracker?>)result).InputValue.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveTracker_Tracked_ReturnsWrittenAndDeletes()
    {
        await this._sut.AddTracker(_userId, _modId, _guildId, _channelId, TimeSpan.FromDays(1));

        var result = await this._sut.RemoveTracker(_userId, _guildId);

        result.ShouldBeOfType<SettingsWritten<Tracker?>>();
        ((SettingsWritten<Tracker?>)result).InputValue.ShouldNotBeNull();

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.GetTrackerChannelAsync(_userId, _guildId)).ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAllExpiredTrackers_DeletesOnlyExpired()
    {
        var activeUserId = new UserId(101UL);
        await this._sut.AddTracker(_userId, _modId, _guildId, _channelId, TimeSpan.FromHours(-1));
        await this._sut.AddTracker(activeUserId, _modId, _guildId, _channelId, TimeSpan.FromDays(1));

        var removed = await this._sut.RemoveAllExpiredTrackers();

        removed.Count.ShouldBe(1);
        removed.Single().UserId.ShouldBe(_userId);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.GetTrackerChannelAsync(activeUserId, _guildId)).ShouldBe(_channelId);
    }

    [Fact]
    public async Task RemoveAllExpiredTrackers_InvalidatesCachePerGuild()
    {
        var guildB = new GuildId(2UL);
        var memberB = new UserId(101UL);
        var channelB = new ChannelId(201UL);

        await this._sut.AddTracker(_userId, _modId, _guildId, _channelId, TimeSpan.FromHours(-1));
        await this._sut.AddTracker(memberB, _modId, guildB, channelB, TimeSpan.FromDays(1));

        // Warm both caches.
        await this._sut.GetTrackerChannelAsync(_userId, _guildId);
        await this._sut.GetTrackerChannelAsync(memberB, guildB);

        await this._sut.RemoveAllExpiredTrackers();

        // Guild A's cache was invalidated; fresh read reflects no tracker.
        (await this._sut.GetTrackerChannelAsync(_userId, _guildId)).ShouldBeNull();
        // Guild B's tracker is unaffected.
        (await this._sut.GetTrackerChannelAsync(memberB, guildB)).ShouldBe(channelB);
    }
}

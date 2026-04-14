// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Lock = Grimoire.Settings.Domain.Lock;

namespace Grimoire.Settings.Tests.Moderation;

[Collection("Settings collection")]
public sealed class LockTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly ChannelId _channelId = new(200UL);
    private static readonly PreviouslyAllowedPermissions _prevAllowed = new(0L);
    private static readonly PreviouslyDeniedPermissions _prevDenied = new(0L);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task NoLock_IsChannelLocked_ReturnsFalse()
    {
        var result = await this._sut.IsChannelLocked(_channelId, _guildId);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AddLock_IsChannelLocked_ReturnsTrue()
    {
        await this._sut.AddLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, "test",
            DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.IsChannelLocked(_channelId, _guildId);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AddLock_ExistingLock_UpdatesInPlace()
    {
        var newEndTime = DateTimeOffset.UtcNow.AddDays(1);
        var newReason = "updated reason";

        await this._sut.AddLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, "original",
            DateTimeOffset.UtcNow.AddHours(1));
        await this._sut.AddLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, newReason, newEndTime);

        await using var db = factory.CreateDbContext();
        var count = await db.Locks.Where(x => x.ChannelId == _channelId && x.GuildId == _guildId).CountAsync();
        count.ShouldBe(1);

        var lockRow = await db.Locks.SingleAsync(x => x.ChannelId == _channelId);
        lockRow.Reason.ShouldBe(newReason);
        lockRow.EndTime.ShouldBe(newEndTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RemoveLock_NotLocked_ReturnsUnchanged()
    {
        var result = await this._sut.RemoveLock(_channelId, _guildId);

        result.ShouldBeOfType<SettingsUnchanged<Lock?>>();
        ((SettingsUnchanged<Lock?>)result).InputValue.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveLock_Locked_ReturnsWrittenAndDeletes()
    {
        await this._sut.AddLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, "test",
            DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.RemoveLock(_channelId, _guildId);

        result.ShouldBeOfType<SettingsWritten<Lock?>>();
        ((SettingsWritten<Lock?>)result).InputValue.ShouldNotBeNull();

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.IsChannelLocked(_channelId, _guildId)).ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllExpiredLocks_OnlyReturnsPastEndTime()
    {
        var futureChannel = new ChannelId(201UL);
        await this._sut.AddLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, "past",
            DateTimeOffset.UtcNow.AddHours(-1));
        await this._sut.AddLock(_modId, _guildId, futureChannel, _prevAllowed, _prevDenied, "future",
            DateTimeOffset.UtcNow.AddHours(1));

        var expired = await this._sut.GetAllExpiredLocks().ToListAsync();

        expired.Count.ShouldBe(1);
        expired.Single().ChannelId.ShouldBe(_channelId);
    }

    [Fact]
    public async Task CacheInvalidatedAfterAddAndRemove()
    {
        await this._sut.AddLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, "test",
            DateTimeOffset.UtcNow.AddHours(1));

        // Warm the cache.
        (await this._sut.IsChannelLocked(_channelId, _guildId)).ShouldBeTrue();

        await this._sut.RemoveLock(_channelId, _guildId);

        // Cache was invalidated by RemoveLock; fresh read reflects removal.
        (await this._sut.IsChannelLocked(_channelId, _guildId)).ShouldBeFalse();
    }
}

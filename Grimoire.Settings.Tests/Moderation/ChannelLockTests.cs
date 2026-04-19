// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.Moderation;

[Collection("Settings collection")]
public sealed class ChannelLockTests(SettingsTestsFactory factory) : IAsyncLifetime
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
    public async Task AddChannelLock_IsChannelLocked_ReturnsTrue()
    {
        await this._sut.AddChannelLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, "test",
            DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.IsChannelLocked(_channelId, _guildId);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AddChannelLock_ExistingLock_InsertsNewRow()
    {
        var newEndTime = DateTimeOffset.UtcNow.AddDays(1);
        var newReason = "updated reason";

        await this._sut.AddChannelLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, "original",
            DateTimeOffset.UtcNow.AddHours(1));
        await this._sut.AddChannelLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, newReason, newEndTime);

        await using var db = factory.CreateDbContext();
        var count = await db.ChannelLocks.CountAsync(x => x.ChannelId == _channelId && x.GuildId == _guildId);
        count.ShouldBe(2);

        var latest = await db.ChannelLocks
            .OfType<ChannelLocked>()
            .Where(x => x.ChannelId == _channelId)
            .OrderByDescending(x => x.SetAt)
            .FirstAsync();
        latest.Reason?.Value.ShouldBe(newReason);
        latest.EndTime.ShouldBe(newEndTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AddChannelLock_ExistingLock_CarriesForwardOriginalPermissions()
    {
        var originalAllowed = new PreviouslyAllowedPermissions(12345L);
        var originalDenied = new PreviouslyDeniedPermissions(67890L);

        await this._sut.AddChannelLock(_modId, _guildId, _channelId, originalAllowed, originalDenied, "first",
            DateTimeOffset.UtcNow.AddHours(1));
        await this._sut.AddChannelLock(_modId, _guildId, _channelId,
            new PreviouslyAllowedPermissions(99999L), new PreviouslyDeniedPermissions(88888L),
            "second", DateTimeOffset.UtcNow.AddHours(2));

        await using var db = factory.CreateDbContext();
        var latest = await db.ChannelLocks
            .OfType<ChannelLocked>()
            .Where(x => x.ChannelId == _channelId)
            .OrderByDescending(x => x.SetAt)
            .FirstAsync();
        latest.PreviouslyAllowed.ShouldBe(originalAllowed);
        latest.PreviouslyDenied.ShouldBe(originalDenied);
    }

    [Fact]
    public async Task RemoveChannelLock_NotLocked_ReturnsNotFound()
    {
        var result = await this._sut.RemoveChannelLock(_channelId, _guildId, _modId);

        result.ShouldBeOfType<Result<ChannelLocked>.NotFound>();
    }

    [Fact]
    public async Task RemoveChannelLock_Locked_ReturnsSuccessAndInsertsUnlockEntry()
    {
        await this._sut.AddChannelLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, "test",
            DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.RemoveChannelLock(_channelId, _guildId, _modId);

        result.ShouldBeOfType<Result<ChannelLocked>.Success>();
        ((Result<ChannelLocked>.Success)result).Value.ShouldNotBeNull();

        await using var db = factory.CreateDbContext();
        var count = await db.ChannelLocks.CountAsync(x => x.ChannelId == _channelId);
        count.ShouldBe(2);
        (await db.ChannelLocks.OfType<ChannelUnlocked>().AnyAsync(x => x.ChannelId == _channelId)).ShouldBeTrue();

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.IsChannelLocked(_channelId, _guildId)).ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllExpiredChannelLocks_OnlyReturnsPastEndTime()
    {
        var futureChannel = new ChannelId(201UL);
        var setAt = DateTimeOffset.UtcNow.AddHours(-2);

        await using var db = factory.CreateDbContext();
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("past"), _channelId, _guildId, setAt,
            _prevAllowed, _prevDenied, setAt.AddHours(1)).OrElse(null!));
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("future"), futureChannel, _guildId, DateTimeOffset.UtcNow,
            _prevAllowed, _prevDenied, DateTimeOffset.UtcNow.AddHours(1)).OrElse(null!));
        await db.SaveChangesAsync();

        var expired = await this._sut.GetAllExpiredChannelLocks().ToListAsync();

        expired.Count.ShouldBe(1);
        expired.Single().ChannelId.ShouldBe(_channelId);
    }

    [Fact]
    public async Task CacheInvalidatedAfterAddAndRemove()
    {
        await this._sut.AddChannelLock(_modId, _guildId, _channelId, _prevAllowed, _prevDenied, "test",
            DateTimeOffset.UtcNow.AddHours(1));

        (await this._sut.IsChannelLocked(_channelId, _guildId)).ShouldBeTrue();

        await this._sut.RemoveChannelLock(_channelId, _guildId, _modId);

        (await this._sut.IsChannelLocked(_channelId, _guildId)).ShouldBeFalse();
    }
}

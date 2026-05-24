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

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    private Task<Result<ChannelLocked>> Lock(
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset endTime,
        string reason = "test",
        PreviouslyAllowedPermissions? allowed = null,
        PreviouslyDeniedPermissions? denied = null)
        => this._sut.ApplyChannelLockAction(
            ChannelLocked.Create(_modId, ModerationReason.FromDatabase(reason),
                channelId, guildId, DateTimeOffset.UtcNow,
                allowed ?? _prevAllowed, denied ?? _prevDenied, endTime).ShouldSucceed());

    private Task<Result<ChannelLocked>> Unlock(ChannelId channelId, GuildId guildId)
        => this._sut.ApplyChannelLockAction(
            ChannelUnlocked.Create(_modId, channelId, guildId, DateTimeOffset.UtcNow).ShouldSucceed());

    [Fact]
    public async Task NoLock_IsChannelLocked_ReturnsFalse()
    {
        var result = await this._sut.IsChannelLocked(_channelId, _guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AddChannelLock_IsChannelLocked_ReturnsTrue()
    {
        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.IsChannelLocked(_channelId, _guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AddChannelLock_ExistingLock_InsertsNewRow()
    {
        var newEndTime = DateTimeOffset.UtcNow.AddDays(1);
        var newReason = "updated reason";

        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1), "original");
        await Lock(_channelId, _guildId, newEndTime, newReason);

        await using var db = factory.CreateDbContext();
        var count = await db.ChannelLocks.CountAsync(x => x.ChannelId == _channelId && x.GuildId == _guildId,
            TestContext.Current.CancellationToken);
        count.ShouldBe(2);

        var latest = await db.ChannelLocks
            .OfType<ChannelLocked>()
            .Where(x => x.ChannelId == _channelId)
            .OrderByDescending(x => x.SetAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        latest.Reason?.Value.ShouldBe(newReason);
        latest.EndTime.ShouldBe(newEndTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AddChannelLock_ExistingLock_CarriesForwardOriginalPermissions()
    {
        var originalAllowed = new PreviouslyAllowedPermissions(12345L);
        var originalDenied = new PreviouslyDeniedPermissions(67890L);

        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1), "first", originalAllowed, originalDenied);
        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(2), "second",
            new PreviouslyAllowedPermissions(99999L), new PreviouslyDeniedPermissions(88888L));

        await using var db = factory.CreateDbContext();
        var latest = await db.ChannelLocks
            .OfType<ChannelLocked>()
            .Where(x => x.ChannelId == _channelId)
            .OrderByDescending(x => x.SetAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        latest.PreviouslyAllowed.ShouldBe(originalAllowed);
        latest.PreviouslyDenied.ShouldBe(originalDenied);
    }

    [Fact]
    public async Task RemoveChannelLock_NotLocked_ReturnsNotFound()
    {
        var result = await Unlock(_channelId, _guildId);

        result.ShouldBeOfType<Result<ChannelLocked>.NotFound>();
    }

    [Fact]
    public async Task RemoveChannelLock_Locked_ReturnsSuccessAndInsertsUnlockEntry()
    {
        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1));

        var result = await Unlock(_channelId, _guildId);

        result.ShouldBeOfType<Result<ChannelLocked>.Success>();
        ((Result<ChannelLocked>.Success)result).Value.ShouldNotBeNull();

        await using var db = factory.CreateDbContext();
        var count = await db.ChannelLocks.CountAsync(x => x.ChannelId == _channelId,
            TestContext.Current.CancellationToken);
        count.ShouldBe(2);
        (await db.ChannelLocks.OfType<ChannelUnlocked>()
            .AnyAsync(x => x.ChannelId == _channelId, TestContext.Current.CancellationToken)).ShouldBeTrue();

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.IsChannelLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllExpiredChannelLocks_OnlyReturnsPastEndTime()
    {
        var futureChannel = new ChannelId(201UL);
        var setAt = DateTimeOffset.UtcNow.AddHours(-2);

        await using var db = factory.CreateDbContext();
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("past"), _channelId, _guildId, setAt,
            _prevAllowed, _prevDenied, setAt.AddHours(1)).ShouldSucceed());
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("future"), futureChannel, _guildId, DateTimeOffset.UtcNow,
            _prevAllowed, _prevDenied, DateTimeOffset.UtcNow.AddHours(1)).ShouldSucceed());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredChannelLocks(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        expired.Count.ShouldBe(1);
        expired.Single().ChannelId.ShouldBe(_channelId);
    }

    [Fact]
    public async Task CacheInvalidatedAfterAddAndRemove()
    {
        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1));

        (await this._sut.IsChannelLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeTrue();

        await Unlock(_channelId, _guildId);

        (await this._sut.IsChannelLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveChannelLock_NotFound_HasCorrectErrorCode()
    {
        var result = await Unlock(_channelId, _guildId);

        var notFound = result.ShouldBeOfType<Result<ChannelLocked>.NotFound>();
        notFound.Error.Code.ShouldBe("channel-lock.not-found");
    }

    [Fact]
    public async Task CacheInvalidated_AfterAddChannelLock()
    {
        (await this._sut.IsChannelLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeFalse();

        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1));

        (await this._sut.IsChannelLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeTrue();
    }

    [Fact]
    public async Task CacheKey_TwoGuilds_NoCacheInterference()
    {
        var guildB = new GuildId(2UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);

        await using var db = factory.CreateDbContext();
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("locked"), _channelId, _guildId, t1,
            _prevAllowed, _prevDenied, t1.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await this._sut.IsChannelLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeTrue();
        (await this._sut.IsChannelLocked(_channelId, guildB, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeFalse();
    }

    [Fact]
    public async Task IsChannelLocked_SameChannelDifferentGuild_ReturnsFalse()
    {
        var guildB = new GuildId(2UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);

        await using var db = factory.CreateDbContext();
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("locked"), _channelId, guildB, t1,
            _prevAllowed, _prevDenied, t1.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await this._sut.IsChannelLocked(_channelId, _guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsChannelLocked_NewerEventForOtherChannelInSameGuild_OriginalChannelStillLocked()
    {
        var otherChannel = new ChannelId(201UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("first"), _channelId, _guildId, t1,
            _prevAllowed, _prevDenied, t1.AddHours(4)).ShouldSucceed());
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("other"), otherChannel, _guildId, t2,
            _prevAllowed, _prevDenied, t2.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.IsChannelLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveChannelLock_DifferentGuild_ReturnsNotFound()
    {
        var guildB = new GuildId(2UL);

        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1), "locked");

        var result = await Unlock(_channelId, guildB);

        result.ShouldBeOfType<Result<ChannelLocked>.NotFound>();
    }

    [Fact]
    public async Task RemoveChannelLock_NewerEventForOtherChannelInSameGuild_OriginalChannelUnlocks()
    {
        var otherChannel = new ChannelId(201UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("first"), _channelId, _guildId, t1,
            _prevAllowed, _prevDenied, t1.AddHours(4)).ShouldSucceed());
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("other"), otherChannel, _guildId, t2,
            _prevAllowed, _prevDenied, t2.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Unlock(_channelId, _guildId);

        result.ShouldBeOfType<Result<ChannelLocked>.Success>();
    }

    [Fact]
    public async Task GetAllExpiredChannelLocks_NewerEventForOtherChannelInSameGuild_ExpiredChannelStillReturned()
    {
        var otherChannel = new ChannelId(201UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-3);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("expired"), _channelId, _guildId, t1,
            _prevAllowed, _prevDenied, t1.AddHours(1)).ShouldSucceed());
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("other"), otherChannel, _guildId, t2,
            _prevAllowed, _prevDenied, t2.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredChannelLocks(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        expired.ShouldContain(x => x.ChannelId == _channelId);
    }

    [Fact]
    public async Task GetAllExpiredChannelLocks_SameChannelDifferentGuild_EachGuildTrackedSeparately()
    {
        var guildB = new GuildId(2UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-3);

        await using var db = factory.CreateDbContext();
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("guild-a"), _channelId, _guildId, t1,
            _prevAllowed, _prevDenied, t1.AddHours(1)).ShouldSucceed());
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("guild-b"), _channelId, guildB, t1.AddMinutes(10),
            _prevAllowed, _prevDenied, t1.AddHours(2)).ShouldSucceed());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredChannelLocks(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        expired.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddChannelLock_TwoChannelsSameGuild_CarriesForwardCorrectPermissions()
    {
        var otherChannel = new ChannelId(201UL);
        var p1Allowed = new PreviouslyAllowedPermissions(11111L);
        var p1Denied = new PreviouslyDeniedPermissions(22222L);
        var p2Allowed = new PreviouslyAllowedPermissions(33333L);
        var p2Denied = new PreviouslyDeniedPermissions(44444L);

        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1), "first", p1Allowed, p1Denied);
        await Lock(otherChannel, _guildId, DateTimeOffset.UtcNow.AddHours(1), "second", p2Allowed, p2Denied);

        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(2), "re-lock",
            new PreviouslyAllowedPermissions(99999L), new PreviouslyDeniedPermissions(88888L));

        await using var db = factory.CreateDbContext();
        var latest = await db.ChannelLocks
            .OfType<ChannelLocked>()
            .Where(x => x.ChannelId == _channelId)
            .OrderByDescending(x => x.SetAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        latest.PreviouslyAllowed.ShouldBe(p1Allowed);
        latest.PreviouslyDenied.ShouldBe(p1Denied);
    }

    [Fact]
    public async Task GetAllExpiredChannelLocks_NewerLockedRowForSameChannel_ExcludedFromExpired()
    {
        var t1 = DateTimeOffset.UtcNow.AddHours(-3);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("old-expired"), _channelId, _guildId, t1,
            _prevAllowed, _prevDenied, t1.AddHours(1)).ShouldSucceed());
        db.ChannelLocks.Add(ChannelLocked.Create(
            _modId, ModerationReason.FromDatabase("newer-active"), _channelId, _guildId, t2,
            _prevAllowed, _prevDenied, t2.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredChannelLocks(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        expired.ShouldBeEmpty();
    }
}

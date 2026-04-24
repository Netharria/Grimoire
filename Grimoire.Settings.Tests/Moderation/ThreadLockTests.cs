// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.Moderation;

[Collection("Settings collection")]
public sealed class ThreadLockTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly ChannelId _channelId = new(300UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task NoLock_IsThreadLocked_ReturnsFalse()
    {
        var result = await this._sut.IsThreadLocked(_channelId, _guildId).ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AddThreadLock_IsThreadLocked_ReturnsTrue()
    {
        await this._sut.AddThreadLock(_modId, _guildId, _channelId, "test", DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.IsThreadLocked(_channelId, _guildId).ShouldSucceed();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AddThreadLock_ExistingLock_InsertsNewRow()
    {
        var newEndTime = DateTimeOffset.UtcNow.AddDays(1);
        var newReason = "updated reason";

        await this._sut.AddThreadLock(_modId, _guildId, _channelId, "original", DateTimeOffset.UtcNow.AddHours(1));
        await this._sut.AddThreadLock(_modId, _guildId, _channelId, newReason, newEndTime);

        await using var db = factory.CreateDbContext();
        var count = await db.ThreadLocks.CountAsync(x => x.ChannelId == _channelId && x.GuildId == _guildId);
        count.ShouldBe(2);

        var latest = await db.ThreadLocks
            .OfType<ThreadLocked>()
            .Where(x => x.ChannelId == _channelId)
            .OrderByDescending(x => x.SetAt)
            .FirstAsync();
        latest.Reason?.Value.ShouldBe(newReason);
        latest.EndTime.ShouldBe(newEndTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RemoveThreadLock_NotLocked_ReturnsNotFound()
    {
        var result = await this._sut.RemoveThreadLock(_channelId, _guildId, _modId);

        result.ShouldBeOfType<Result<ThreadLocked>.NotFound>();
    }

    [Fact]
    public async Task RemoveThreadLock_Locked_ReturnsSuccessAndInsertsUnlockEntry()
    {
        await this._sut.AddThreadLock(_modId, _guildId, _channelId, "test", DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.RemoveThreadLock(_channelId, _guildId, _modId);

        result.ShouldBeOfType<Result<ThreadLocked>.Success>();
        ((Result<ThreadLocked>.Success)result).Value.ShouldNotBeNull();

        await using var db = factory.CreateDbContext();
        var count = await db.ThreadLocks.CountAsync(x => x.ChannelId == _channelId);
        count.ShouldBe(2);
        (await db.ThreadLocks.OfType<ThreadUnlocked>().AnyAsync(x => x.ChannelId == _channelId)).ShouldBeTrue();

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.IsThreadLocked(_channelId, _guildId)).ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllExpiredThreadLocks_OnlyReturnsPastEndTime()
    {
        var futureChannel = new ChannelId(301UL);
        var setAt = DateTimeOffset.UtcNow.AddHours(-2);

        await using var db = factory.CreateDbContext();
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("past"), _channelId, _guildId, setAt,
            setAt.AddHours(1)).ShouldSucceed());
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("future"), futureChannel, _guildId, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1)).ShouldSucceed());
        await db.SaveChangesAsync();

        var expired = await this._sut.GetAllExpiredThreadLocks().ToListAsync();

        expired.Count.ShouldBe(1);
        expired.Single().ChannelId.ShouldBe(_channelId);
    }

    [Fact]
    public async Task CacheInvalidatedAfterAddAndRemove()
    {
        await this._sut.AddThreadLock(_modId, _guildId, _channelId, "test", DateTimeOffset.UtcNow.AddHours(1));

        (await this._sut.IsThreadLocked(_channelId, _guildId)).ShouldSucceed().ShouldBeTrue();

        await this._sut.RemoveThreadLock(_channelId, _guildId, _modId);

        (await this._sut.IsThreadLocked(_channelId, _guildId)).ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task AddThreadLock_InvalidReason_ReturnsInvalidWithErrorCode()
    {
        var result = await this._sut.AddThreadLock(_modId, _guildId, _channelId, "   ",
            DateTimeOffset.UtcNow.AddHours(1));

        var invalid = result.ShouldBeOfType<Result<ThreadLocked>.Invalid>();
        invalid.Errors.ShouldContain(e => e.Code == "moderation-reason.invalid");
    }

    [Fact]
    public async Task AddThreadLock_PastEndTime_ReturnsInvalidWithErrorCode()
    {
        var result = await this._sut.AddThreadLock(_modId, _guildId, _channelId, "reason",
            DateTimeOffset.UtcNow.AddHours(-1));

        var invalid = result.ShouldBeOfType<Result<ThreadLocked>.Invalid>();
        invalid.Errors.ShouldContain(e => e.Code == "thread-lock.end-time.invalid");
    }

    [Fact]
    public async Task RemoveThreadLock_NotFound_HasCorrectErrorCode()
    {
        var result = await this._sut.RemoveThreadLock(_channelId, _guildId, _modId);

        var notFound = result.ShouldBeOfType<Result<ThreadLocked>.NotFound>();
        notFound.Error.Code.ShouldBe("thread-lock.not-found");
    }

    [Fact]
    public async Task CacheInvalidated_AfterAddThreadLock()
    {
        (await this._sut.IsThreadLocked(_channelId, _guildId)).ShouldSucceed().ShouldBeFalse();

        await this._sut.AddThreadLock(_modId, _guildId, _channelId, "test", DateTimeOffset.UtcNow.AddHours(1));

        (await this._sut.IsThreadLocked(_channelId, _guildId)).ShouldSucceed().ShouldBeTrue();
    }

    [Fact]
    public async Task CacheKey_TwoGuilds_NoCacheInterference()
    {
        var guildB = new GuildId(2UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);

        await using var db = factory.CreateDbContext();
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("locked"), _channelId, _guildId, t1,
            t1.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync();

        (await this._sut.IsThreadLocked(_channelId, _guildId)).ShouldSucceed().ShouldBeTrue();
        (await this._sut.IsThreadLocked(_channelId, guildB)).ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task IsThreadLocked_SameChannelDifferentGuild_ReturnsFalse()
    {
        var guildB = new GuildId(2UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);

        await using var db = factory.CreateDbContext();
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("locked"), _channelId, guildB, t1,
            t1.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync();

        (await this._sut.IsThreadLocked(_channelId, _guildId)).ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task IsThreadLocked_NewerEventForOtherChannelInSameGuild_OriginalChannelStillLocked()
    {
        var otherChannel = new ChannelId(301UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("first"), _channelId, _guildId, t1,
            t1.AddHours(4)).ShouldSucceed());
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("other"), otherChannel, _guildId, t2,
            t2.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync();

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.IsThreadLocked(_channelId, _guildId)).ShouldSucceed().ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveThreadLock_DifferentGuild_ReturnsNotFound()
    {
        var guildB = new GuildId(2UL);

        await this._sut.AddThreadLock(_modId, _guildId, _channelId, "locked", DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.RemoveThreadLock(_channelId, guildB, _modId);

        result.ShouldBeOfType<Result<ThreadLocked>.NotFound>();
    }

    [Fact]
    public async Task RemoveThreadLock_NewerEventForOtherChannelInSameGuild_OriginalChannelUnlocks()
    {
        var otherChannel = new ChannelId(301UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("first"), _channelId, _guildId, t1,
            t1.AddHours(4)).ShouldSucceed());
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("other"), otherChannel, _guildId, t2,
            t2.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync();

        var result = await this._sut.RemoveThreadLock(_channelId, _guildId, _modId);

        result.ShouldBeOfType<Result<ThreadLocked>.Success>();
    }

    [Fact]
    public async Task GetAllExpiredThreadLocks_NewerEventForOtherChannelInSameGuild_ExpiredChannelStillReturned()
    {
        var otherChannel = new ChannelId(301UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-3);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("expired"), _channelId, _guildId, t1,
            t1.AddHours(1)).ShouldSucceed());
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("other"), otherChannel, _guildId, t2,
            t2.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync();

        var expired = await this._sut.GetAllExpiredThreadLocks().ToListAsync();

        expired.ShouldContain(x => x.ChannelId == _channelId);
    }

    [Fact]
    public async Task GetAllExpiredThreadLocks_NewerLockedRowForSameChannel_ExcludedFromExpired()
    {
        var t1 = DateTimeOffset.UtcNow.AddHours(-3);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("old-expired"), _channelId, _guildId, t1,
            t1.AddHours(1)).ShouldSucceed());
        db.ThreadLocks.Add(ThreadLocked.Create(
            _modId, ModerationReason.FromDatabase("newer-active"), _channelId, _guildId, t2,
            t2.AddHours(4)).ShouldSucceed());
        await db.SaveChangesAsync();

        var expired = await this._sut.GetAllExpiredThreadLocks().ToListAsync();

        expired.ShouldBeEmpty();
    }
}

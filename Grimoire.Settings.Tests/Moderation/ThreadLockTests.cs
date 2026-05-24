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

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    private Task<Result<ThreadLocked>> Lock(ChannelId channelId, GuildId guildId, DateTimeOffset endTime,
        string reason = "test")
        => this._sut.ApplyThreadLockAction(
            ThreadLocked.Create(_modId, ModerationReason.FromDatabase(reason),
                channelId, guildId, DateTimeOffset.UtcNow, endTime).ShouldSucceed());

    private Task<Result<ThreadLocked>> Unlock(ChannelId channelId, GuildId guildId)
        => this._sut.ApplyThreadLockAction(
            ThreadUnlocked.Create(_modId, channelId, guildId, DateTimeOffset.UtcNow).ShouldSucceed());

    [Fact]
    public async Task NoLock_IsThreadLocked_ReturnsFalse()
    {
        var result = await this._sut.IsThreadLocked(_channelId, _guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AddThreadLock_IsThreadLocked_ReturnsTrue()
    {
        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.IsThreadLocked(_channelId, _guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AddThreadLock_ExistingLock_InsertsNewRow()
    {
        var newEndTime = DateTimeOffset.UtcNow.AddDays(1);
        var newReason = "updated reason";

        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1), "original");
        await Lock(_channelId, _guildId, newEndTime, newReason);

        await using var db = factory.CreateDbContext();
        var count = await db.ThreadLocks.CountAsync(x => x.ChannelId == _channelId && x.GuildId == _guildId,
            TestContext.Current.CancellationToken);
        count.ShouldBe(2);

        var latest = await db.ThreadLocks
            .OfType<ThreadLocked>()
            .Where(x => x.ChannelId == _channelId)
            .OrderByDescending(x => x.SetAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        latest.Reason?.Value.ShouldBe(newReason);
        latest.EndTime.ShouldBe(newEndTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RemoveThreadLock_NotLocked_ReturnsNotFound()
    {
        var result = await Unlock(_channelId, _guildId);

        result.ShouldBeOfType<Result<ThreadLocked>.NotFound>();
    }

    [Fact]
    public async Task RemoveThreadLock_Locked_ReturnsSuccessAndInsertsUnlockEntry()
    {
        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1));

        var result = await Unlock(_channelId, _guildId);

        result.ShouldBeOfType<Result<ThreadLocked>.Success>();
        ((Result<ThreadLocked>.Success)result).Value.ShouldNotBeNull();

        await using var db = factory.CreateDbContext();
        var count = await db.ThreadLocks.CountAsync(x => x.ChannelId == _channelId,
            TestContext.Current.CancellationToken);
        count.ShouldBe(2);
        (await db.ThreadLocks.OfType<ThreadUnlocked>()
            .AnyAsync(x => x.ChannelId == _channelId, TestContext.Current.CancellationToken)).ShouldBeTrue();

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.IsThreadLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeFalse();
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredThreadLocks(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        expired.Count.ShouldBe(1);
        expired.Single().ChannelId.ShouldBe(_channelId);
    }

    [Fact]
    public async Task CacheInvalidatedAfterAddAndRemove()
    {
        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1));

        (await this._sut.IsThreadLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeTrue();

        await Unlock(_channelId, _guildId);

        (await this._sut.IsThreadLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveThreadLock_NotFound_HasCorrectErrorCode()
    {
        var result = await Unlock(_channelId, _guildId);

        var notFound = result.ShouldBeOfType<Result<ThreadLocked>.NotFound>();
        notFound.Error.Code.ShouldBe("thread-lock.not-found");
    }

    [Fact]
    public async Task CacheInvalidated_AfterAddThreadLock()
    {
        (await this._sut.IsThreadLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeFalse();

        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1));

        (await this._sut.IsThreadLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeTrue();
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await this._sut.IsThreadLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeTrue();
        (await this._sut.IsThreadLocked(_channelId, guildB, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeFalse();
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await this._sut.IsThreadLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeFalse();
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        (await freshSut.IsThreadLocked(_channelId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveThreadLock_DifferentGuild_ReturnsNotFound()
    {
        var guildB = new GuildId(2UL);

        await Lock(_channelId, _guildId, DateTimeOffset.UtcNow.AddHours(1), "locked");

        var result = await Unlock(_channelId, guildB);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Unlock(_channelId, _guildId);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredThreadLocks(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredThreadLocks(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        expired.ShouldBeEmpty();
    }
}

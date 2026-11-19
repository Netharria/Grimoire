// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

using Grimoire.IntegrationTests.Database.SettingsLayer;

namespace Grimoire.IntegrationTests.Database.SettingsLayer.Moderation;

[Collection("Settings collection")]
public sealed class MuteTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly UserId _userId = new(100UL);
    private static readonly RoleId _roleId = new(300UL);
    private static readonly SinId _sinId = new(1L);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    [Fact]
    public async Task AddMute_NewMember_InsertsRow()
    {
        var result = await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);

        result.ShouldSucceed();

        await using var db = factory.CreateDbContext();
        var count = await db.Mutes.Where(x => x.UserId == _userId && x.GuildId == _guildId)
            .CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    /// <summary>Review S-13: a mute of a muted member closes the old mute, and an unmute closes the new one.</summary>
    [Fact]
    public async Task AddMuteThenRemoveMute_CloseEachMuteInTurn()
    {
        var token = TestContext.Current.CancellationToken;
        (await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), null,
            token)).ShouldSucceed();
        var second = (await this._sut.AddMute(_userId, _guildId, _modId, new SinId(_sinId.Value + 1),
            DateTimeOffset.UtcNow.AddHours(2), null, token)).ShouldSucceed();
        (await this._sut.GetActiveMute(_userId, _guildId, token)).ShouldSucceed().SetAt
            .ShouldBe(second.SetAt, TimeSpan.FromMilliseconds(1));

        (await this._sut.RemoveMute(_userId, _guildId, _modId, token)).ShouldSucceed();

        await using var db = factory.CreateDbContext();
        var mutes = await db.Mutes.OrderBy(x => x.SetAt).ToListAsync(token);
        mutes[0].State.ShouldBe("Removed");
        mutes[0].EndedAt.ShouldNotBeNull().ShouldBe(second.SetAt, TimeSpan.FromMilliseconds(1));
        mutes[1].State.ShouldBe("Removed");
        (await this._sut.GetActiveMute(_userId, _guildId, token)).ShouldFail().ShouldBeOfType<NotFound>();
    }

    [Fact]
    public async Task AddMute_ExistingMember_AppendsRow()
    {
        var newSinId = new SinId(2L);
        var newEndTime = DateTimeOffset.UtcNow.AddDays(1);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);
        var result = await this._sut.AddMute(_userId, _guildId, _modId, newSinId, newEndTime, null,
            TestContext.Current.CancellationToken);

        result.ShouldSucceed();

        await using var db = factory.CreateDbContext();
        var count = await db.Mutes.Where(x => x.UserId == _userId && x.GuildId == _guildId)
            .CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(2);

        var latestMute = await db.Mutes
            .Where(x => x.UserId == _userId && x.GuildId == _guildId)
            .OrderByDescending(x => x.SetAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        latestMute.EndTime.ShouldBe(newEndTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RemoveMute_NotMuted_ReturnsNotFound()
    {
        var result = await this._sut.RemoveMute(_userId, _guildId, _modId, TestContext.Current.CancellationToken);

        result.ShouldFail().ShouldBeOfType<NotFound>();
    }

    [Fact]
    public async Task RemoveMute_Muted_ReturnsSuccessAndMarksTheMuteRemoved()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);

        var result = await this._sut.RemoveMute(_userId, _guildId, _modId, TestContext.Current.CancellationToken);

        result.ShouldSucceed();
        ((Fin<Mute>.Succ)result).Value.ShouldNotBeNull();

        await using var db = factory.CreateDbContext();
        var removed = await db.Mutes.SingleAsync(x => x.UserId == _userId && x.GuildId == _guildId,
            TestContext.Current.CancellationToken);
        removed.State.ShouldBe("Removed");
        removed.RemovedBy.ShouldBe(_modId);
        (await this._sut.GetActiveMute(_userId, _guildId, TestContext.Current.CancellationToken))
            .ShouldFail().ShouldBeOfType<NotFound>();
    }

    [Fact]
    public async Task GetAllExpiredMutes_OnlyReturnsPastEndTime()
    {
        var pastUser = new UserId(101UL);
        var futureUser = new UserId(102UL);
        var recentUser = new UserId(103UL);

        await using (var db = factory.CreateDbContext())
        {
            var pastMute = SettingsRows.Mute(pastUser, _guildId, _modId, new SinId(10L),
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1));
            var recentMute = SettingsRows.Mute(recentUser, _guildId, _modId, new SinId(12L),
                DateTimeOffset.UtcNow.AddSeconds(-10), DateTimeOffset.UtcNow.AddSeconds(-5));
            db.Mutes.Add(pastMute);
            db.Mutes.Add(recentMute);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await this._sut.AddMute(futureUser, _guildId, _modId, new SinId(11L), DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredMutes(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        expired.Count.ShouldBe(2);
        expired.ShouldContain(x => x.UserId == pastUser);
        expired.ShouldContain(x => x.UserId == recentUser);
    }

    [Fact]
    public async Task GetAllExpiredMutes_SkipsUnmutedMembers()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(-1), null,
            TestContext.Current.CancellationToken);
        await this._sut.RemoveMute(_userId, _guildId, _modId, TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredMutes(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        expired.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllMutes_ReturnsAllForGuild_IgnoresOtherGuilds()
    {
        var guildB = new GuildId(2UL);
        var userB = new UserId(101UL);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);
        await this._sut.AddMute(userB, guildB, _modId, new SinId(2L), DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);

        var mutes = await this._sut.GetAllMutes(_guildId, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        mutes.Count.ShouldBe(1);
        mutes.Single().UserId.ShouldBe(_userId);
    }

    [Fact]
    public async Task GetAllMutes_ExcludesUnmutedMembers()
    {
        var userB = new UserId(101UL);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);
        await this._sut.AddMute(userB, _guildId, _modId, new SinId(2L), DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);
        await this._sut.RemoveMute(_userId, _guildId, _modId, TestContext.Current.CancellationToken);

        var mutes = await this._sut.GetAllMutes(_guildId, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        mutes.Count.ShouldBe(1);
        mutes.Single().UserId.ShouldBe(userB);
    }

    [Fact]
    public async Task AddMute_StoresTheMuteRoleItWasGivenWith()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), _roleId,
            TestContext.Current.CancellationToken);

        var mute = await this._sut.GetActiveMute(_userId, _guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        mute.MuteRoleId.ShouldBe(_roleId);
    }

    [Fact]
    public async Task AddMute_WithoutARole_LeavesItEmpty()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);

        (await this._sut.GetActiveMute(_userId, _guildId, TestContext.Current.CancellationToken).ShouldSucceed())
            .MuteRoleId.ShouldBeNull();
    }

    [Fact]
    public async Task ExpiredMutes_CarryTheRoleTheyWereMutedWith()
    {
        // AddMute cannot create a mute that has already ended, so insert one directly.
        await using (var db = factory.CreateDbContext())
        {
            db.Mutes.Add(SettingsRows.Mute(_userId, _guildId, _modId, _sinId,
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1), _roleId));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var expired = await this._sut.GetAllExpiredMutes(TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        expired.ShouldHaveSingleItem().MuteRoleId.ShouldBe(_roleId);
    }

    [Fact]
    public async Task GetActiveMute_ReturnsTheLatestMutesRole()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), _roleId,
            TestContext.Current.CancellationToken);
        var newRole = new RoleId(301UL);
        await this._sut.AddMute(_userId, _guildId, _modId, new SinId(2L), DateTimeOffset.UtcNow.AddHours(2), newRole,
            TestContext.Current.CancellationToken);

        (await this._sut.GetActiveMute(_userId, _guildId, TestContext.Current.CancellationToken).ShouldSucceed())
            .MuteRoleId.ShouldBe(newRole);
    }

    [Fact]
    public async Task GetActiveMute_WithoutAMute_IsNotFound()
        => (await this._sut.GetActiveMute(_userId, _guildId, TestContext.Current.CancellationToken))
            .ShouldFail().ShouldBeOfType<NotFound>();

    [Fact]
    public async Task GetActiveMute_AfterTheMuteWasRemoved_IsNotFound()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), _roleId,
            TestContext.Current.CancellationToken);
        await this._sut.RemoveMute(_userId, _guildId, _modId, TestContext.Current.CancellationToken);

        (await this._sut.GetActiveMute(_userId, _guildId, TestContext.Current.CancellationToken))
            .ShouldFail().ShouldBeOfType<NotFound>();
    }

    [Fact]
    public async Task GetActiveMute_OtherGuild_IsNotFound()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), _roleId,
            TestContext.Current.CancellationToken);

        (await this._sut.GetActiveMute(_userId, new GuildId(2UL), TestContext.Current.CancellationToken))
            .ShouldFail().ShouldBeOfType<NotFound>();
    }

    [Fact]
    public async Task AddMute_InvalidEndTime_ReturnsInvalidWithErrorCode()
    {
        var result = await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(-1), null,
            TestContext.Current.CancellationToken);

        var invalid = result.ShouldFail().ShouldBeOfType<Invalid>();
        invalid.Message.ShouldBe("Mute parameters are invalid.");
    }

    [Fact]
    public async Task RemoveMute_NotFound_HasCorrectErrorCode()
    {
        var result = await this._sut.RemoveMute(_userId, _guildId, _modId, TestContext.Current.CancellationToken);

        var notFound = result.ShouldFail().ShouldBeOfType<NotFound>();
        notFound.Message.ShouldBe("No active mute found for this user.");
    }

    [Fact]
    public async Task RemoveMute_SameUserDifferentGuild_ReturnsNotFound()
    {
        var guildB = new GuildId(2UL);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);

        var result = await this._sut.RemoveMute(_userId, guildB, _modId, TestContext.Current.CancellationToken);

        result.ShouldFail().ShouldBeOfType<NotFound>();
    }

    [Fact]
    public async Task GetActiveMute_NewerMuteRemovedInDifferentGuild_OriginalGuildStillMuted()
    {
        var guildB = new GuildId(2UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.Mutes.Add(SettingsRows.Mute(
            _userId, _guildId, _modId, _sinId,
            t1, t1.AddHours(4)));
        db.Mutes.Add(SettingsRows.RemovedMute(_userId, guildB, _modId, new SinId(99L), t2.AddMinutes(-30), t2));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await this._sut.GetActiveMute(_userId, _guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .SetAt.ShouldBe(t1, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetAllExpiredMutes_SameUserDifferentGuild_EachGuildTrackedSeparately()
    {
        var guildB = new GuildId(2UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-3);

        await using var db = factory.CreateDbContext();
        db.Mutes.Add(SettingsRows.Mute(
            _userId, _guildId, _modId, _sinId,
            t1, t1.AddHours(1)));
        db.Mutes.Add(SettingsRows.Mute(
            _userId, guildB, _modId, new SinId(2L),
            t1.AddMinutes(10), t1.AddHours(2)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredMutes(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        expired.Count.ShouldBe(2);
        expired.ShouldContain(x => x.GuildId == _guildId);
        expired.ShouldContain(x => x.GuildId == guildB);
    }

    [Fact]
    public async Task GetAllExpiredMutes_NewerMuteRemovedInDifferentGuild_OriginalExpiredMuteStillReturned()
    {
        var guildB = new GuildId(2UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-3);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.Mutes.Add(SettingsRows.Mute(
            _userId, _guildId, _modId, _sinId,
            t1, t1.AddHours(1)));
        db.Mutes.Add(SettingsRows.RemovedMute(_userId, guildB, _modId, new SinId(99L), t2.AddMinutes(-30), t2));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expired = await this._sut.GetAllExpiredMutes(TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        expired.ShouldContain(x => x.UserId == _userId && x.GuildId == _guildId);
    }

    [Fact]
    public async Task GetAllMutes_SameUserDifferentGuild_ExcludesOtherGuild()
    {
        var guildB = new GuildId(2UL);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);
        await this._sut.AddMute(_userId, guildB, _modId, new SinId(2L), DateTimeOffset.UtcNow.AddHours(1), null,
            TestContext.Current.CancellationToken);

        var mutes = await this._sut.GetAllMutes(_guildId, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        mutes.Count.ShouldBe(1);
        mutes.Single().UserId.ShouldBe(_userId);
        mutes.Single().GuildId.ShouldBe(_guildId);
    }

    [Fact]
    public async Task ModuleDisabled_GetEffectiveMuteRole_ReturnsNull()
    {
        await this._sut.SetMuteRole(_guildId, _modId, _roleId, TestContext.Current.CancellationToken);

        var result = await this._sut.GetEffectiveMuteRole(_guildId, TestContext.Current.CancellationToken);

        result.ShouldSucceed().IsNone.ShouldBeTrue();
    }

    [Fact]
    public async Task SetMuteRole_ModuleEnabled_GetEffectiveReturnsRole()
    {
        await this._sut.SetModuleState(Module.Moderation, _guildId, _modId, true,
            TestContext.Current.CancellationToken);
        await this._sut.SetMuteRole(_guildId, _modId, _roleId, TestContext.Current.CancellationToken);

        var result = await this._sut.GetEffectiveMuteRole(_guildId, TestContext.Current.CancellationToken);

        result.ShouldSucceed().ShouldBe(_roleId);
    }

    [Fact]
    public async Task DisableMuteRole_GetEffectiveReturnsNull()
    {
        await this._sut.SetModuleState(Module.Moderation, _guildId, _modId, true,
            TestContext.Current.CancellationToken);
        await this._sut.SetMuteRole(_guildId, _modId, _roleId, TestContext.Current.CancellationToken);
        await this._sut.DisableMuteRole(_guildId, _modId, TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetEffectiveMuteRole(_guildId, TestContext.Current.CancellationToken);

        result.ShouldSucceed().IsNone.ShouldBeTrue();
    }
}

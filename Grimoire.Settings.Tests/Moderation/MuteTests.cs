// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.Moderation;

[Collection("Settings collection")]
public sealed class MuteTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly UserId _userId = new(100UL);
    private static readonly RoleId _roleId = new(300UL);
    private static readonly SinId _sinId = new(1L);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task NoMute_IsMemberMuted_ReturnsFalse()
    {
        var result = await this._sut.IsMemberMuted(_userId, _guildId).ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ActiveMute_IsMemberMuted_ReturnsTrue()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.IsMemberMuted(_userId, _guildId).ShouldSucceed();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExpiredMute_IsMemberMuted_ReturnsFalse()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(-1));

        var result = await this._sut.IsMemberMuted(_userId, _guildId).ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AddMute_NewMember_InsertsRow()
    {
        var result = await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1));

        result.ShouldBeOfType<Result<MuteAdded>.Success>();

        await using var db = factory.CreateDbContext();
        var count = await db.Mutes.Where(x => x.UserId == _userId && x.GuildId == _guildId).CountAsync();
        count.ShouldBe(1);
    }

    [Fact]
    public async Task AddMute_ExistingMember_AppendsRow()
    {
        var newSinId = new SinId(2L);
        var newEndTime = DateTimeOffset.UtcNow.AddDays(1);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1));
        var result = await this._sut.AddMute(_userId, _guildId, _modId, newSinId, newEndTime);

        result.ShouldBeOfType<Result<MuteAdded>.Success>();

        await using var db = factory.CreateDbContext();
        var count = await db.Mutes.Where(x => x.UserId == _userId && x.GuildId == _guildId).CountAsync();
        count.ShouldBe(2);

        var latestMute = await db.Mutes
            .OfType<MuteAdded>()
            .Where(x => x.UserId == _userId && x.GuildId == _guildId)
            .OrderByDescending(x => x.SetAt)
            .FirstAsync();
        latestMute.EndTime.ShouldBe(newEndTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RemoveMute_NotMuted_ReturnsNotFound()
    {
        var result = await this._sut.RemoveMute(_userId, _guildId, _modId);

        result.ShouldBeOfType<Result<MuteAdded>.NotFound>();
    }

    [Fact]
    public async Task RemoveMute_Muted_ReturnsSuccessAndAppendsMuteRemoved()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.RemoveMute(_userId, _guildId, _modId);

        result.ShouldBeOfType<Result<MuteAdded>.Success>();
        ((Result<MuteAdded>.Success)result).Value.ShouldNotBeNull();

        await using var db = factory.CreateDbContext();
        (await db.Mutes.OfType<MuteRemoved>().AnyAsync(x => x.UserId == _userId && x.GuildId == _guildId))
            .ShouldBeTrue();
        (await this._sut.IsMemberMuted(_userId, _guildId)).ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllExpiredMutes_OnlyReturnsPastEndTime()
    {
        var pastUser = new UserId(101UL);
        var futureUser = new UserId(102UL);
        var recentUser = new UserId(103UL);

        await using (var db = factory.CreateDbContext())
        {
            var pastMute = MuteAdded.Create(pastUser, _guildId, _modId, new SinId(10L),
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1));
            var recentMute = MuteAdded.Create(recentUser, _guildId, _modId, new SinId(12L),
                DateTimeOffset.UtcNow.AddSeconds(-10), DateTimeOffset.UtcNow.AddSeconds(-5));
            db.Mutes.Add(((Validation<MuteAdded>.Valid)pastMute).Value);
            db.Mutes.Add(((Validation<MuteAdded>.Valid)recentMute).Value);
            await db.SaveChangesAsync();
        }
        await this._sut.AddMute(futureUser, _guildId, _modId, new SinId(11L), DateTimeOffset.UtcNow.AddHours(1));

        var expired = await this._sut.GetAllExpiredMutes().ToListAsync();

        expired.Count.ShouldBe(2);
        expired.ShouldContain(x => x.UserId == pastUser);
        expired.ShouldContain(x => x.UserId == recentUser);
    }

    [Fact]
    public async Task GetAllExpiredMutes_SkipsUnmutedMembers()
    {
        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(-1));
        await this._sut.RemoveMute(_userId, _guildId, _modId);

        var expired = await this._sut.GetAllExpiredMutes().ToListAsync();

        expired.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllMutes_ReturnsAllForGuild_IgnoresOtherGuilds()
    {
        var guildB = new GuildId(2UL);
        var userB = new UserId(101UL);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1));
        await this._sut.AddMute(userB, guildB, _modId, new SinId(2L), DateTimeOffset.UtcNow.AddHours(1));

        var mutes = await this._sut.GetAllMutes(_guildId).ToListAsync();

        mutes.Count.ShouldBe(1);
        mutes.Single().UserId.ShouldBe(_userId);
    }

    [Fact]
    public async Task GetAllMutes_ExcludesUnmutedMembers()
    {
        var userB = new UserId(101UL);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1));
        await this._sut.AddMute(userB, _guildId, _modId, new SinId(2L), DateTimeOffset.UtcNow.AddHours(1));
        await this._sut.RemoveMute(_userId, _guildId, _modId);

        var mutes = await this._sut.GetAllMutes(_guildId).ToListAsync();

        mutes.Count.ShouldBe(1);
        mutes.Single().UserId.ShouldBe(userB);
    }

    [Fact]
    public async Task AddMute_InvalidEndTime_ReturnsInvalidWithErrorCode()
    {
        var result = await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(-1));

        var invalid = result.ShouldBeOfType<Result<MuteAdded>.Invalid>();
        invalid.Errors.ShouldContain(e => e.Code == "mute.invalid");
    }

    [Fact]
    public async Task RemoveMute_NotFound_HasCorrectErrorCode()
    {
        var result = await this._sut.RemoveMute(_userId, _guildId, _modId);

        var notFound = result.ShouldBeOfType<Result<MuteAdded>.NotFound>();
        notFound.Error.Code.ShouldBe("mute.not-found");
    }

    [Fact]
    public async Task IsMemberMuted_SameUserDifferentGuild_ReturnsFalse()
    {
        var guildB = new GuildId(2UL);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1));

        (await this._sut.IsMemberMuted(_userId, guildB)).ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveMute_SameUserDifferentGuild_ReturnsNotFound()
    {
        var guildB = new GuildId(2UL);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1));

        var result = await this._sut.RemoveMute(_userId, guildB, _modId);

        result.ShouldBeOfType<Result<MuteAdded>.NotFound>();
    }

    [Fact]
    public async Task IsMemberMuted_NewerMuteRemovedInDifferentGuild_OriginalGuildStillMuted()
    {
        var guildB = new GuildId(2UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);

        await using var db = factory.CreateDbContext();
        db.Mutes.Add(((Validation<MuteAdded>.Valid)MuteAdded.Create(
            _userId, _guildId, _modId, _sinId,
            t1, t1.AddHours(4))).Value);
        db.Mutes.Add(((Validation<MuteRemoved>.Valid)MuteRemoved.Create(_userId, guildB, _modId, t2)).Value);
        await db.SaveChangesAsync();

        (await this._sut.IsMemberMuted(_userId, _guildId)).ShouldSucceed().ShouldBeTrue();
    }

    [Fact]
    public async Task GetAllExpiredMutes_SameUserDifferentGuild_EachGuildTrackedSeparately()
    {
        var guildB = new GuildId(2UL);
        var t1 = DateTimeOffset.UtcNow.AddHours(-3);

        await using var db = factory.CreateDbContext();
        db.Mutes.Add(((Validation<MuteAdded>.Valid)MuteAdded.Create(
            _userId, _guildId, _modId, _sinId,
            t1, t1.AddHours(1))).Value);
        db.Mutes.Add(((Validation<MuteAdded>.Valid)MuteAdded.Create(
            _userId, guildB, _modId, new SinId(2L),
            t1.AddMinutes(10), t1.AddHours(2))).Value);
        await db.SaveChangesAsync();

        var expired = await this._sut.GetAllExpiredMutes().ToListAsync();

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
        db.Mutes.Add(((Validation<MuteAdded>.Valid)MuteAdded.Create(
            _userId, _guildId, _modId, _sinId,
            t1, t1.AddHours(1))).Value);
        db.Mutes.Add(((Validation<MuteRemoved>.Valid)MuteRemoved.Create(_userId, guildB, _modId, t2)).Value);
        await db.SaveChangesAsync();

        var expired = await this._sut.GetAllExpiredMutes().ToListAsync();

        expired.ShouldContain(x => x.UserId == _userId && x.GuildId == _guildId);
    }

    [Fact]
    public async Task GetAllMutes_SameUserDifferentGuild_ExcludesOtherGuild()
    {
        var guildB = new GuildId(2UL);

        await this._sut.AddMute(_userId, _guildId, _modId, _sinId, DateTimeOffset.UtcNow.AddHours(1));
        await this._sut.AddMute(_userId, guildB, _modId, new SinId(2L), DateTimeOffset.UtcNow.AddHours(1));

        var mutes = await this._sut.GetAllMutes(_guildId).ToListAsync();

        mutes.Count.ShouldBe(1);
        mutes.Single().UserId.ShouldBe(_userId);
        mutes.Single().GuildId.ShouldBe(_guildId);
    }

    [Fact]
    public async Task ModuleDisabled_GetEffectiveMuteRole_ReturnsNull()
    {
        await this._sut.SetMuteRole(_guildId, _modId, _roleId);

        var result = await this._sut.GetEffectiveMuteRole(_guildId);

        result.ShouldSucceed().ShouldBeNull();
    }

    [Fact]
    public async Task SetMuteRole_ModuleEnabled_GetEffectiveReturnsRole()
    {
        await this._sut.SetModuleState(Module.Moderation, _guildId, _modId, true);
        await this._sut.SetMuteRole(_guildId, _modId, _roleId);

        var result = await this._sut.GetEffectiveMuteRole(_guildId);

        result.ShouldSucceed().ShouldBe(_roleId);
    }

    [Fact]
    public async Task DisableMuteRole_GetEffectiveReturnsNull()
    {
        await this._sut.SetModuleState(Module.Moderation, _guildId, _modId, true);
        await this._sut.SetMuteRole(_guildId, _modId, _roleId);
        await this._sut.DisableMuteRole(_guildId, _modId);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetEffectiveMuteRole(_guildId);

        result.ShouldSucceed().ShouldBeNull();
    }
}

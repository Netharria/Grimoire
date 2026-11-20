// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Domain.Values;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Grimoire.IntegrationTests.Database.SettingsLayer.Leveling;

[Collection("Settings collection")]
public sealed class RewardsTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly RoleId _roleId = new(300UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public async ValueTask InitializeAsync()
        => await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);

    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    private Task<Fin<Write<RewardChange>>> AddReward(RoleId roleId, int level, GuildId? guildId = null)
        => this._sut.SetRewardAsync(guildId ?? _guildId, _modId, new RewardEntry(roleId, level, null),
            TestContext.Current.CancellationToken);

    private Task<Fin<Write<Set<RoleId>>>> RemoveReward(RoleId roleId)
        => this._sut.RemoveRewardsAsync(_guildId, _modId, [roleId], TestContext.Current.CancellationToken);

    [Fact]
    public async Task NoRewards_ReturnsEmptySet()
    {
        var result = await this._sut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SingleReward_ReturnedCorrectly()
    {
        (await this._sut.SetRewardAsync(_guildId, _modId, new RewardEntry(_roleId, 5, RewardMessage.FromDatabase("GG")),
            TestContext.Current.CancellationToken)).ShouldBeSaved().ShouldBeOfType<RewardChange.Added>();

        var result = await this._sut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldHaveSingleItem();
        var entry = result.Single();
        entry.RoleId.ShouldBe(_roleId);
        entry.RewardLevel.ShouldBe(5);
        entry.RewardMessage?.Value.ShouldBe("GG");
    }

    [Fact]
    public async Task MultipleRoles_AllReturned()
    {
        var roleId2 = new RoleId(301UL);
        var roleId3 = new RoleId(302UL);
        await AddReward(_roleId, 5);
        await AddReward(roleId2, 10);
        await AddReward(roleId3, 15);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.Count.ShouldBe(3);
        result.Select(x => x.RoleId).ShouldContain(_roleId);
        result.Select(x => x.RoleId).ShouldContain(roleId2);
        result.Select(x => x.RoleId).ShouldContain(roleId3);
    }

    [Fact]
    public async Task AChangedLevel_IsMoved_AndReadBack()
    {
        await AddReward(_roleId, 5);
        (await AddReward(_roleId, 10)).ShouldBeSaved().ShouldBe(new RewardChange.Moved(5));

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldHaveSingleItem();
        result.Single().RewardLevel.ShouldBe(10);
    }

    [Fact]
    public async Task ARemovedReward_IsNotReturned()
    {
        await AddReward(_roleId, 5);
        (await RemoveReward(_roleId)).ShouldBeSaved().ShouldBe(toSet([_roleId]));

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReAdded_AppearsAgain()
    {
        await AddReward(_roleId, 5);
        await RemoveReward(_roleId);
        await AddReward(_roleId, 5);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldHaveSingleItem();
        result.Single().RoleId.ShouldBe(_roleId);
    }

    [Fact]
    public async Task CacheInvalidated_AfterSetReward()
    {
        (await this._sut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeEmpty();

        await AddReward(_roleId, 5);

        (await this._sut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TwoGuilds_Rewards_Independent()
    {
        var guildB = new GuildId(2UL);
        var roleB = new RoleId(301UL);
        await this._sut.SetModuleState(Module.Leveling, guildB, _modId, true, TestContext.Current.CancellationToken);

        await AddReward(_roleId, 5);
        await AddReward(roleB, 10, guildB);

        var resultA = await this._sut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();
        var resultB = await this._sut.GetLevelingRewardsAsync(guildB, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        resultA.ShouldHaveSingleItem();
        resultA.Single().RoleId.ShouldBe(_roleId);
        resultB.ShouldHaveSingleItem();
        resultB.Single().RoleId.ShouldBe(roleB);
    }

    [Fact]
    public async Task TheRewardAlreadySet_IsUnchanged_AndStoresNothing()
    {
        await AddReward(_roleId, 5);

        (await AddReward(_roleId, 5)).ShouldBeUnchanged();

        await using var db = factory.CreateDbContext();
        (await SettingsRows.LatestDocument(db, _guildId, SettingsDocument.Rewards,
            TestContext.Current.CancellationToken))!.Version.ShouldBe(1);
    }

    [Fact]
    public async Task RemovingARoleWithNoReward_IsUnchanged()
    {
        await AddReward(_roleId, 5);

        (await RemoveReward(new RoleId(301UL))).ShouldBeUnchanged();
    }

    /// <summary>
    ///     Spec 2.5: two removals of the same reward at once. One returns the role and the other is unchanged, decided
    ///     against the version each replaced, so a deleted role is never reported twice.
    /// </summary>
    [Fact]
    public async Task ConcurrentRemovals_OfTheSameReward_RemoveItOnce()
    {
        await AddReward(_roleId, 5);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            SettingsModuleFactory.Create(factory.ConnectionString).RemoveRewardsAsync(_guildId, _modId, [_roleId],
                TestContext.Current.CancellationToken)));

        results.Count(result => result.ShouldSucceed() is Write<Set<RoleId>>.Saved).ShouldBe(1);
        results.Count(result => result.ShouldSucceed() is Write<Set<RoleId>>.Unchanged).ShouldBe(1);
    }
}

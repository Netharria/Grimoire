// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Tests.Leveling;

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

    private Task AddReward(RoleId roleId, int level, string? message = null)
        => this._sut.SetRewardAsync(
            new RewardAdded(roleId, _guildId, _modId, DateTimeOffset.UtcNow, level, null));

    private Task RemoveReward(RoleId roleId)
        => this._sut.SetRewardAsync(
            ((Validation<RewardRemoved>.Valid)RewardRemoved.Create(roleId, _guildId, _modId, DateTimeOffset.UtcNow))
            .Value);

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
        await this._sut.SetRewardAsync(
            new RewardAdded(_roleId, _guildId, _modId, DateTimeOffset.UtcNow, 5, RewardMessage.FromDatabase("GG")),
            TestContext.Current.CancellationToken);

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
    public async Task MultipleHistoricalRows_LatestWins()
    {
        await AddReward(_roleId, 5);
        await AddReward(_roleId, 10);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldHaveSingleItem();
        result.Single().RewardLevel.ShouldBe(10);
    }

    [Fact]
    public async Task LatestRowRemoved_NotReturned()
    {
        await AddReward(_roleId, 5);
        await RemoveReward(_roleId);

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
        await this._sut.SetRewardAsync(new RewardAdded(roleB, guildB, _modId, DateTimeOffset.UtcNow, 10, null),
            TestContext.Current.CancellationToken);

        var resultA = await this._sut.GetLevelingRewardsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed();
        var resultB = await this._sut.GetLevelingRewardsAsync(guildB, TestContext.Current.CancellationToken)
            .ShouldSucceed();

        resultA.ShouldHaveSingleItem();
        resultA.Single().RoleId.ShouldBe(_roleId);
        resultB.ShouldHaveSingleItem();
        resultB.Single().RoleId.ShouldBe(roleB);
    }
}

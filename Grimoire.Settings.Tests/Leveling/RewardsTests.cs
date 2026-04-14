// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.Leveling;

[Collection("Settings collection")]
public sealed class RewardsTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly RoleId _roleId = new(300UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public async Task InitializeAsync()
        => await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);

    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task NoRewards_ReturnsEmptySet()
    {
        var result = await this._sut.GetLevelingRewardsAsync(_guildId);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ModuleDisabled_ReturnsEmptySet()
    {
        await this._sut.SetRewardAsync(_roleId, _guildId, _modId, 5, null, true);
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, false);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetLevelingRewardsAsync(_guildId);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SingleReward_ReturnedCorrectly()
    {
        await this._sut.SetRewardAsync(_roleId, _guildId, _modId, 5, "GG", true);

        var result = await this._sut.GetLevelingRewardsAsync(_guildId);

        result.ShouldHaveSingleItem();
        var entry = result.Single();
        entry.RoleId.ShouldBe(_roleId);
        entry.RewardLevel.ShouldBe(5);
        entry.RewardMessage.ShouldBe("GG");
    }

    [Fact]
    public async Task MultipleRoles_AllReturned()
    {
        var roleId2 = new RoleId(301UL);
        var roleId3 = new RoleId(302UL);
        await this._sut.SetRewardAsync(_roleId, _guildId, _modId, 5, null, true);
        await this._sut.SetRewardAsync(roleId2, _guildId, _modId, 10, null, true);
        await this._sut.SetRewardAsync(roleId3, _guildId, _modId, 15, null, true);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetLevelingRewardsAsync(_guildId);

        result.Count.ShouldBe(3);
        result.Select(x => x.RoleId).ShouldContain(_roleId);
        result.Select(x => x.RoleId).ShouldContain(roleId2);
        result.Select(x => x.RoleId).ShouldContain(roleId3);
    }

    [Fact]
    public async Task MultipleHistoricalRows_LatestWins()
    {
        // Two rows for the same role; the second (level=10) is latest.
        await this._sut.SetRewardAsync(_roleId, _guildId, _modId, 5, null, true);
        await this._sut.SetRewardAsync(_roleId, _guildId, _modId, 10, null, true);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetLevelingRewardsAsync(_guildId);

        result.ShouldHaveSingleItem();
        result.Single().RewardLevel.ShouldBe(10);
    }

    [Fact]
    public async Task LatestRowDisabled_NotReturned()
    {
        await this._sut.SetRewardAsync(_roleId, _guildId, _modId, 5, null, true);
        await this._sut.SetRewardAsync(_roleId, _guildId, _modId, 5, null, false);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetLevelingRewardsAsync(_guildId);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReEnable_AppearsAgain()
    {
        await this._sut.SetRewardAsync(_roleId, _guildId, _modId, 5, null, true);
        await this._sut.SetRewardAsync(_roleId, _guildId, _modId, 5, null, false);
        await this._sut.SetRewardAsync(_roleId, _guildId, _modId, 5, null, true);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetLevelingRewardsAsync(_guildId);

        result.ShouldHaveSingleItem();
        result.Single().RoleId.ShouldBe(_roleId);
    }
}

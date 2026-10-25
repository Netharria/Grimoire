// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     <c>/Rewards</c> Add, Remove and View (backlog A-28). Reward changes are logged to the leveling log, which
///     points at <c>#modlog</c>.
/// </summary>
[Collection("Test collection")]
public sealed class RewardCommandsTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
        (await this.Settings.SetLogChannelSetting(GuildLogType.Leveling, this.GuildId, new ModeratorId(this.Guild.Owner.Id),
            new ChannelId(this.Guild.ModLog.Id))).ShouldBeSetOrUnchanged();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    [Fact]
    public async Task Add_StoresTheReward_AndLogsIt()
    {
        var expected = $"Successfully updated the rewards to include <@&{this.Guild.KickerRoleId}> at level 3.";

        (await this.AddAsync(this.Guild.KickerRoleId, 3, "well done")).Description.ShouldBe(expected);

        var reward = (await this.RewardsAsync()).ShouldHaveSingleItem();
        (reward.RoleId, reward.RewardLevel, reward.RewardMessage?.Value).ShouldBe((new RoleId(this.Guild.KickerRoleId), 3, "well done"));
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>() == expected);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Add_ForARoleThatIsAlreadyAReward_UpdatesIt()
    {
        await this.AddAsync(this.Guild.KickerRoleId, 3, "old");

        await this.AddAsync(this.Guild.KickerRoleId, 7, null);

        var reward = (await this.RewardsAsync()).ShouldHaveSingleItem();
        (reward.RewardLevel, reward.RewardMessage).ShouldBe((7, null));
    }

    [Fact]
    public async Task Add_ARoleAboveTheBot_IsRefused()
    {
        (await this.AddAsync(this.Guild.HigherRoleId, 3, null)).Description.ShouldBe(
            "Was not able to update the rewards for the server:\n" +
            "Grimoire will not be able to apply this reward role because the role has a higher rank than it does.");

        (await this.RewardsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task View_ListsEveryReward()
    {
        await this.AddAsync(this.Guild.KickerRoleId, 3, "hi");
        await this.AddAsync(this.Guild.BannerRoleId, 5, null);

        var reply = await this.RewardsCommandAsync("view");

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Rewards");
        reply.Description.ShouldNotBeNull().Split('\n').ShouldBe(
            [$"Level:3 Role:<@&{this.Guild.KickerRoleId}> Reward Message: hi", $"Level:5 Role:<@&{this.Guild.BannerRoleId}> "],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Remove_DeletesTheReward_AndLogsIt()
    {
        await this.AddAsync(this.Guild.KickerRoleId, 3, null);
        var expected = $"{this.Guild.Admin.MemberMention} removed <@&{this.Guild.KickerRoleId}> reward";

        (await this.RewardsCommandAsync("remove", SlashOption.Role("role", this.Guild.KickerRoleId))).Description.ShouldBe(expected);

        (await this.RewardsAsync()).ShouldBeEmpty();
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>() == expected);
    }

    /// <summary>
    ///     <c>/Rewards</c> is registered for everyone, so each subcommand must check Manage Guild itself. Add and View
    ///     do.
    /// </summary>
    [Fact]
    public async Task Add_ByAMemberWithoutManageGuild_IsRefused()
        => (await this._bot.RunSlashCommandAndWaitAsync("rewards", this.Guild.Moderator, SlashOption.Subcommand("add",
                SlashOption.Role("role", this.Guild.KickerRoleId), SlashOption.Integer("level", 3))))
            .Description.ShouldBe("You do not have the required permissions to use this command.");

    [Fact(Skip = "Fails: /Rewards is registered for everyone and Remove has no permission check, so any member can " +
                 "remove rewards. KI-45")]
    public async Task Remove_ByAMemberWithoutManageGuild_IsRefused()
    {
        await this.AddAsync(this.Guild.KickerRoleId, 3, null);

        (await this._bot.RunSlashCommandAndWaitAsync("rewards", this.Guild.User1,
                SlashOption.Subcommand("remove", SlashOption.Role("role", this.Guild.KickerRoleId))))
            .Description.ShouldBe("You do not have the required permissions to use this command.");
        (await this.RewardsAsync()).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("add")]
    [InlineData("view")]
    public async Task Rewards_NeedTheModule(string subcommand)
    {
        await this._bot.SetModuleAsync(Module.Leveling, false);

        (await this.RewardsCommandAsync(subcommand, subcommand == "add"
                ? [SlashOption.Role("role", this.Guild.KickerRoleId), SlashOption.Integer("level", 3)]
                : []))
            .Description.ShouldBe("This module is disabled in this server.");
    }

    [Fact(Skip = "Fails: /Rewards Remove has none of the group's checks, so it runs with the leveling module off " +
                 "and for anyone Discord lets see the command. KI-45")]
    public async Task Remove_NeedsTheModule()
    {
        await this.AddAsync(this.Guild.KickerRoleId, 3, null);
        await this._bot.SetModuleAsync(Module.Leveling, false);

        (await this.RewardsCommandAsync("remove", SlashOption.Role("role", this.Guild.KickerRoleId)))
            .Description.ShouldBe("This module is disabled in this server.");
    }

    private Task<FakeReply> RewardsCommandAsync(string subcommand, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("rewards", this.Guild.Admin, SlashOption.Subcommand(subcommand, options));

    private Task<FakeReply> AddAsync(ulong roleId, int level, string? message)
        => this.RewardsCommandAsync("add",
        [
            SlashOption.Role("role", roleId), SlashOption.Integer("level", level),
            .. message is null ? Array.Empty<SlashOption>() : [SlashOption.String("message", message)]
        ]);

    private async Task<IReadOnlySet<Grimoire.Settings.Domain.RewardEntry>> RewardsAsync()
        => (await this.Settings.GetLevelingRewardsAsync(this.GuildId)).ShouldSucceed();
}

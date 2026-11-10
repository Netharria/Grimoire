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

    /// <remarks>Cases: LVL-100.</remarks>
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

    /// <remarks>Cases: LVL-101.</remarks>
    [Fact]
    public async Task Add_ForARoleThatIsAlreadyAReward_UpdatesIt()
    {
        await this.AddAsync(this.Guild.KickerRoleId, 3, "old");

        await this.AddAsync(this.Guild.KickerRoleId, 7, null);

        var reward = (await this.RewardsAsync()).ShouldHaveSingleItem();
        (reward.RewardLevel, reward.RewardMessage).ShouldBe((7, null));
    }

    /// <remarks>Cases: LVL-103.</remarks>
    [Fact]
    public async Task Add_ARoleAboveTheBot_IsRefused()
    {
        (await this.AddAsync(this.Guild.HigherRoleId, 3, null)).Description.ShouldBe(
            "Was not able to update the rewards for the server:\n" +
            "Grimoire will not be able to apply this reward role because the role has the same or a higher rank than it does.");

        (await this.RewardsAsync()).ShouldBeEmpty();
    }

    /// <summary>X-11: Discord needs the bot's top role strictly above the role, so a role level with it is refused.</summary>
    /// <remarks>Cases: LVL-104.</remarks>
    [Fact]
    public async Task Add_ARoleLevelWithTheBot_IsRefused()
    {
        var level = this._bot.Rest.CreateRole("Level", 0, FakeGuild.RoleAboveTheBot - 1);

        (await this.AddAsync(level, 3, null)).Description.ShouldBe(
            "Was not able to update the rewards for the server:\n" +
            "Grimoire will not be able to apply this reward role because the role has the same or a higher rank than it does.");

        (await this.RewardsAsync()).ShouldBeEmpty();
    }

    /// <summary>X-11: a managed role, such as Server Booster, can't be granted by anyone, so it can't be a reward.</summary>
    /// <remarks>Cases: LVL-105.</remarks>
    [Fact]
    public async Task Add_AManagedRole_IsRefused()
    {
        var booster = this._bot.Rest.CreateRole("Server Booster", managed: true);

        (await this.AddAsync(booster, 3, null)).Description.ShouldBe(
            "Was not able to update the rewards for the server:\n" +
            "That role is managed by an integration, such as a bot or Server Booster, so it can't be given out.");

        (await this.RewardsAsync()).ShouldBeEmpty();
    }

    /// <remarks>Cases: LVL-110.</remarks>
    [Fact]
    public async Task View_ListsEveryReward()
    {
        await this.AddAsync(this.Guild.KickerRoleId, 3, "hi");
        await this.AddAsync(this.Guild.BannerRoleId, 5, null);

        var reply = await this.RewardsCommandAsync("view");

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Rewards");
        reply.Description.ShouldNotBeNull().Split('\n').ShouldBe(
            [$"Level:3 Role:<@&{this.Guild.KickerRoleId}> Reward Message: hi", $"Level:5 Role:<@&{this.Guild.BannerRoleId}>"],
            ignoreOrder: true);
    }

    /// <remarks>Cases: LVL-114.</remarks>
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
    /// <remarks>Cases: LVL-116.</remarks>
    [Fact]
    public async Task Add_ByAMemberWithoutManageGuild_IsRefused()
        => (await this._bot.RunSlashCommandAndWaitAsync("rewards", this.Guild.Moderator, SlashOption.Subcommand("add",
                SlashOption.Role("role", this.Guild.KickerRoleId), SlashOption.Integer("level", 3))))
            .Description.ShouldBe("You do not have the required permissions to use this command.");

    /// <remarks>Cases: LVL-116.</remarks>
    [Fact]
    public async Task Remove_ByAMemberWithoutManageGuild_IsRefused()
    {
        await this.AddAsync(this.Guild.KickerRoleId, 3, null);

        (await this._bot.RunSlashCommandAndWaitAsync("rewards", this.Guild.User1,
                SlashOption.Subcommand("remove", SlashOption.Role("role", this.Guild.KickerRoleId))))
            .Description.ShouldBe("You do not have the required permissions to use this command.");
        (await this.RewardsAsync()).ShouldHaveSingleItem();
    }

    /// <remarks>Cases: LVL-117.</remarks>
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

    /// <remarks>Cases: LVL-117.</remarks>
    [Fact]
    public async Task Remove_NeedsTheModule()
    {
        await this.AddAsync(this.Guild.KickerRoleId, 3, null);
        await this._bot.SetModuleAsync(Module.Leveling, false);

        (await this.RewardsCommandAsync("remove", SlashOption.Role("role", this.Guild.KickerRoleId)))
            .Description.ShouldBe("This module is disabled in this server.");
    }

    /// <summary>
    ///     I-9: only "not found" means a role is gone. /Level looks up the next reward's role, and during an outage it
    ///     used to show "None" as if the role were deleted. Now the command fails, and is reported.
    /// </summary>
    /// <remarks>Cases: LVL-58.</remarks>
    [Fact]
    public async Task Level_DuringADiscordOutage_FailsInsteadOfShowingTheRewardAsMissing()
    {
        const ulong uncachedRole = 1_100_000_000_000_000_399;
        await this.AddRewardDirectlyAsync(uncachedRole, 2, null);
        this._bot.Rest.On(HttpMethod.Get, @"guilds/\d+/roles(/\d+)?",
            _ => FakeDiscordRest.Error(System.Net.HttpStatusCode.ServiceUnavailable, 0, "Service Unavailable"));

        var reply = await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1);

        reply.Description.ShouldNotBeNull().ShouldStartWith("Encountered exception while executing");
    }

    /// <summary>X-10: a deleted role is listed by its id, with no lookup.</summary>
    /// <remarks>Cases: LVL-112.</remarks>
    [Fact]
    public async Task View_ListsADeletedRoleByItsId()
    {
        const ulong deletedRole = 1_100_000_000_000_000_398;
        await this.AddRewardDirectlyAsync(deletedRole, 2, null);

        (await this.RewardsCommandAsync("view")).Description.ShouldBe($"Level:2 Role:<@&{deletedRole}>");
    }

    /// <summary>X-10: an empty list says so, instead of a blank embed.</summary>
    /// <remarks>Cases: LVL-111.</remarks>
    [Fact]
    public async Task View_WithNoRewards_SaysSo()
        => (await this.RewardsCommandAsync("view")).Description.ShouldBe("This server does not have any rewards.");

    /// <summary>
    ///     X-10: two 3,000-character messages don't fit one embed, so the list is paged, and each message is shown
    ///     whole. It used to fail outright.
    /// </summary>
    /// <remarks>Cases: LVL-113.</remarks>
    [Fact]
    public async Task View_WithLongRewardMessages_IsPaged_WithEachMessageWhole()
    {
        await this.AddRewardDirectlyAsync(this.Guild.KickerRoleId, 2, new string('a', 3000));
        await this.AddRewardDirectlyAsync(this.Guild.BannerRoleId, 4, new string('b', 3000));

        var reply = await this.RewardsCommandAsync("view");

        reply.Description.ShouldBe($"Level:2 Role:<@&{this.Guild.KickerRoleId}> Reward Message: {new string('a', 3000)}");
        reply.Embed!["footer"]!["text"]!.GetValue<string>().ShouldBe("Page 1 of 2");
    }

    /// <summary>X-7: a role that isn't a reward can't be removed, and nothing is recorded or logged.</summary>
    /// <remarks>Cases: LVL-115.</remarks>
    [Fact]
    public async Task Remove_OfARoleThatIsNotAReward_IsRefused()
    {
        var reply = await this.RewardsCommandAsync("remove", SlashOption.Role("role", this.Guild.KickerRoleId));

        reply.Description.ShouldNotBeNull().ShouldContain($"Did not find a saved reward for <@&{this.Guild.KickerRoleId}>.");
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Rewards.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    private async Task AddRewardDirectlyAsync(ulong roleId, int level, string? message)
        => (await this.Settings.SetRewardAsync(Grimoire.Settings.Domain.RewardAdded.Create(new RoleId(roleId), this.GuildId, level,
            message is null ? null : Grimoire.Settings.Domain.Values.RewardMessage.Create(message).ShouldSucceed(),
            new ModeratorId(this.Guild.Owner.Id), DateTimeOffset.UtcNow).ShouldSucceed())).ShouldSucceed();

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

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using DSharpPlus;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Values;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     Reward roles on XP gain, from <c>06-leveling.md</c> section B, beyond <see cref="GainXpTests" /> (backlog A-46):
///     several rewards at once, rewards without a message or with DMs closed, rewards already held or removed by hand,
///     a bot without Manage Roles, rewards reached through <c>/Award</c>, and <c>@everyone</c> as a reward (KI-75), plus
///     the <c>/Rewards Add</c> checks LVL-106 and LVL-107. The leveling log points at <c>#modlog</c>. With an amount of
///     15, one message reaches level 2; with 100, level 5.
/// </summary>
[Collection("Test collection")]
public sealed class RewardGrantTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    /// <summary>Enough for the mod log and DMs, without Manage Roles.</summary>
    private const ulong PermissionsWithoutManageRoles = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission
                                                                                        | FakeGuild.EmbedLinksPermission;

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private ModeratorId Owner => new(this.Guild.Owner.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        await this.StartAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>Starts the bot, or restarts it on the same database for a test that needs a different guild.</summary>
    private async Task StartAsync(Action<FakeGuild>? configureGuild = null)
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database, configureGuild);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
        (await this.Settings.SetLogChannelSetting(GuildLogType.Leveling, this.GuildId, this.Owner,
            new ChannelId(this.Guild.LevelLog.Id))).ShouldBeSetOrUnchanged();
    }

    // ── Granting ──────────────────────────────────────────────────────────────

    /// <summary>LVL-32: two rewards reached by one message are both granted, and logged together in one line.</summary>
    [Fact]
    public async Task TwoRewardsReachedAtOnce_AreBothGranted_AndLoggedInOneLine()
    {
        await this.SetAmountAsync(100);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, null);
        await this.AddRewardAsync(this.Guild.BannerRoleId, 3, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "big jump");

        var log = await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            .StartsWith($"{this.Guild.User1.MemberMention} has earned ", StringComparison.Ordinal) == true);
        log["description"]!.GetValue<string>().ShouldContain($"<@&{this.Guild.KickerRoleId}>");
        log["description"]!.GetValue<string>().ShouldContain($"<@&{this.Guild.BannerRoleId}>");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldBe([this.Guild.KickerRoleId, this.Guild.BannerRoleId], ignoreOrder: true);
        await Task.Delay(NothingHappensWindow, Token);
        this.EarnedLogPosts().Count().ShouldBe(1);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LVL-33: a reward without a message is granted and logged, and no DM is sent.</summary>
    [Fact]
    public async Task ARewardWithoutAMessage_IsGranted_WithoutADm()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this.WaitForEarnedLogAsync(this.Guild.User1, this.Guild.KickerRoleId);
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(this.Guild.KickerRoleId);
        (await this._bot.Rest.ReceivesRequestWithinAsync(this.IsDmTo(this.Guild.User1), NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>
    ///     LVL-34: with the member's DMs closed, the reward is still granted and logged. The failed DM is an
    ///     information log, not an error.
    /// </summary>
    [Fact]
    public async Task ARewardForAMemberWithClosedDms_IsStillGranted_AndOnlyLoggedAsInformation()
    {
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Guild.User1.Id + 1000}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50007, "Cannot send messages to this user"));
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, "well done");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this.WaitForEarnedLogAsync(this.Guild.User1, this.Guild.KickerRoleId);
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(this.Guild.KickerRoleId);
        await this._bot.Logs.WaitForAsync(logEvent => logEvent.MessageTemplate.Text.StartsWith(
            "Failure to send reward message", StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LVL-35: a member who already holds the reward role gets no grant and no log line.</summary>
    [Fact]
    public async Task ARewardTheMemberAlreadyHolds_IsNotGrantedOrLoggedAgain()
    {
        await this.StartAsync(guild => guild.GrantRole(guild.User1, guild.KickerRoleId));
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, "well done");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has leveled to level 2.");
        (await this._bot.Rest.ReceivesRequestWithinAsync(this.IsRoleGrant(this.Guild.User1), NothingHappensWindow)).ShouldBeFalse();
        this.EarnedLogPosts().ShouldBeEmpty();
        this._bot.Rest.Requests.ShouldNotContain(request => this.IsDmTo(this.Guild.User1)(request));
    }

    /// <summary>LVL-36: a reward role removed by hand is granted again on the member's next earned message.</summary>
    [Fact]
    public async Task ARewardRoleRemovedByHand_IsGrantedAgain_OnTheNextGain()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, null);
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");
        await this.WaitForEarnedLogAsync(this.Guild.User1, this.Guild.KickerRoleId);

        this._bot.SetRolesByHand(this.Guild.User1);
        await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.MemberRoleCountInTheBotsCache(this.Guild.User1)), count => count == 0);
        await this.BackdateTimeoutsAsync(this.Guild.User1, TimeSpan.FromMinutes(10));
        // 15 to 16 XP stays on level 2, so the grant comes from the retry, not from a level-up.
        await this.SetAmountAsync(1);
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "again");

        await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.EarnedLogPosts().Count()), count => count == 2);
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(this.Guild.KickerRoleId);
    }

    /// <summary>LVL-38: without Manage Roles, the reward isn't granted and the moderators are told.</summary>
    [Fact]
    public async Task ARewardWhileTheBotLacksManageRoles_IsReported()
    {
        await this.StartAsync(guild => guild.BotPermissions = PermissionsWithoutManageRoles);
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.StartsWith(
            $"I could not give the level reward roles <@&{this.Guild.KickerRoleId}>.", StringComparison.Ordinal) == true);
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this.Guild.KickerRoleId);
        this.EarnedLogPosts().ShouldBeEmpty();
    }

    /// <summary>
    ///     LVL-44: XP from <c>/Award</c> doesn't grant a reward by itself; the role comes with the member's next earned
    ///     message.
    /// </summary>
    [Fact]
    public async Task ARewardReachedThroughAward_IsGrantedOnTheNextEarnedMessage()
    {
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, null);

        await this._bot.RunSlashCommandAndWaitAsync("award", this.Guild.Moderator,
            SlashOption.User("user", this.Guild.User1), SlashOption.Integer("xp", 50));

        (await this._bot.Rest.ReceivesRequestWithinAsync(this.IsRoleGrant(this.Guild.User1), NothingHappensWindow)).ShouldBeFalse();
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this.Guild.KickerRoleId);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");

        await this.WaitForEarnedLogAsync(this.Guild.User1, this.Guild.KickerRoleId);
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(this.Guild.KickerRoleId);
    }

    // ── @everyone and setup checks ────────────────────────────────────────────

    /// <summary>
    ///     LVL-43: Discord refuses to grant <c>@everyone</c>. That refusal isn't Unauthorized, so it escapes the grant
    ///     loop: the other reward reached at the same level should still be granted and logged, without an error.
    ///     The fake answers 400 code 50028; Discord's exact answer isn't documented.
    /// </summary>
    [Fact(Skip = "Fails: the grant loop only catches UnauthorizedException, so the refused @everyone grant throws out of the handler and the 'has earned' log is skipped. KI-75")]
    public async Task AnEveryoneReward_DoesNotStopTheOtherRewards()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.Id, 2, null);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this.WaitForEarnedLogAsync(this.Guild.User1, this.Guild.KickerRoleId);
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(this.Guild.KickerRoleId);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LVL-106: <c>@everyone</c> can't be granted, so <c>/Rewards Add</c> should refuse it.</summary>
    [Fact(Skip = "Fails: /Rewards Add checks only managed roles and rank, so @everyone is stored as a reward. KI-75")]
    public async Task AddingEveryoneAsAReward_IsRefused()
    {
        var reply = await this.AddRewardCommandAsync(this.Guild.Id);

        reply.Description.ShouldNotBeNull().ShouldStartWith("Was not able to update the rewards for the server:");
        (await this.Settings.GetLevelingRewardsAsync(this.GuildId, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeEmpty();
    }

    /// <summary>LVL-107: when the bot lacks Manage Roles, <c>/Rewards Add</c> should warn that it can't grant the role.</summary>
    [Fact(Skip = "Fails: /Rewards Add doesn't check Manage Roles; the reward is accepted and fails only when reached. KI-76")]
    public async Task AddingARewardWhileTheBotLacksManageRoles_Warns()
    {
        await this.StartAsync(guild => guild.BotPermissions = PermissionsWithoutManageRoles);

        var reply = await this.AddRewardCommandAsync(this.Guild.KickerRoleId);

        reply.Description.ShouldNotBeNull().ShouldContain("Manage Roles");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<FakeReply> AddRewardCommandAsync(ulong roleId)
        => this._bot.RunSlashCommandAndWaitAsync("rewards", this.Guild.Admin, SlashOption.Subcommand("add",
            SlashOption.Role("role", roleId), SlashOption.Integer("level", 2)));

    private int MemberRoleCountInTheBotsCache(FakeUser user)
        => this._bot.Services.GetRequiredService<DiscordClient>().Guilds[this.Guild.Id].Members.TryGetValue(user.Id, out var member)
            ? member.Roles.Count()
            : -1;

    private Func<RecordedRequest, bool> IsDmTo(FakeUser user)
        => request => request.Method == HttpMethod.Post && request.Path == $"channels/{user.Id + 1000}/messages";

    private Func<RecordedRequest, bool> IsRoleGrant(FakeUser user)
        => request => request.Method == HttpMethod.Put
                      && request.Path.StartsWith($"guilds/{this.Guild.Id}/members/{user.Id}/roles/", StringComparison.Ordinal);

    private IEnumerable<RecordedRequest> EarnedLogPosts()
        => this._bot.Rest.Requests.Where(request => this._bot.IsLevelLogPost(request)
                                                    && request.Body?.Contains("has earned", StringComparison.Ordinal) == true);

    private Task WaitForEarnedLogAsync(FakeUser user, ulong roleId)
        => this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{user.MemberMention} has earned <@&{roleId}>");

    private async Task SetAmountAsync(int amount)
        => (await this.Settings.SetLevelingSettings(this.GuildId, this.Owner, LevelSettings.Amount, amount)).ShouldSucceed();

    private async Task AddRewardAsync(ulong roleId, int level, string? message)
        => (await this.Settings.SetRewardAsync(RewardAdded.Create(new RoleId(roleId), this.GuildId, level,
            message is null ? null : RewardMessage.Create(message).ShouldSucceed(), this.Owner, DateTimeOffset.UtcNow).ShouldSucceed())).ShouldSucceed();

    /// <summary>Moves the member's timeouts back, as if <paramref name="by" /> had passed. The timeout is part of the key.</summary>
    private async Task BackdateTimeoutsAsync(FakeUser user, TimeSpan by)
    {
        await using var dbContext = database.CreateDbContext();
        await dbContext.Database.ExecuteSqlAsync(
            $"""UPDATE "XpHistory" SET "TimeOut" = "TimeOut" - {by} WHERE "UserId" = {(decimal)user.Id} AND "GuildId" = {(decimal)this.Guild.Id}""",
            Token);
    }
}

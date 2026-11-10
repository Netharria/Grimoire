// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Values;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     <c>GainUserXp</c> (backlog A-25). A member's message earns XP once per timeout period, unless the member, one of
///     their roles or the channel is ignored. Crossing a level posts to the leveling log, and reaching a reward's level
///     grants its role, DMs its message and logs it. The leveling log points at <c>#modlog</c>. With the default
///     scaling base of 15, the first level-up is at 15 XP.
/// </summary>
[Collection("Test collection")]
public sealed class GainXpTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private ModeratorId Owner => new(this.Guild.Owner.Id);

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        await this.StartAsync();
    }

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

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Earning XP ────────────────────────────────────────────────────────────

    /// <summary>A message earns the configured amount, with a timeout that ends one timeout period later.</summary>
    /// <remarks>Cases: LVL-01.</remarks>
    [Fact]
    public async Task AMessage_EarnsXp_WithATimeout()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");

        var earned = (await this.WaitForEarnedAsync(this.Guild.User1, 1)).ShouldHaveSingleItem();
        earned.Xp.Value.ShouldBe(XpGainAmount.Default.Value);
        earned.TimeOut.ShouldBe(DateTimeOffset.UtcNow + XpTimeoutPeriod.Default.Value, TimeSpan.FromSeconds(30));
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <remarks>Cases: LVL-02.</remarks>
    [Fact]
    public async Task ASecondMessage_DuringTheTimeout_EarnsNothing()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "one");
        await this.WaitForEarnedAsync(this.Guild.User1, 1);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "two");

        (await this.WaitForEarnedAsync(this.Guild.User1, 2, NothingHappensWindow)).Count.ShouldBe(1);
    }

    /// <summary>Messages from bots, from ignored members, roles and channels, and with the module off, earn nothing.</summary>
    /// <remarks>
    ///     Cases: LVL-05 (bot), LVL-08 (ignored member), LVL-09 (ignored role), LVL-10 (ignored channel), LVL-13
    ///     (module off).
    /// </remarks>
    [Theory]
    [InlineData("bot")]
    [InlineData("ignored member")]
    [InlineData("ignored role")]
    [InlineData("ignored channel")]
    [InlineData("module off")]
    public async Task Messages_ThatShouldNotCount_EarnNothing(string situation)
    {
        var author = situation == "bot" ? this.Guild.Bot : this.Guild.Moderator;
        var now = DateTimeOffset.UtcNow;
        XpIgnoredItem? ignored = situation switch
        {
            "ignored member" => IgnoredMember.Create(new UserId(author.Id), this.GuildId, this.Owner, now),
            "ignored role" => IgnoredRole.Create(new RoleId(this.Guild.ModeratorRoleId), this.GuildId, this.Owner, now),
            "ignored channel" => IgnoredChannel.Create(new ChannelId(this.Guild.General.Id), this.GuildId, this.Owner, now),
            _ => null
        };
        if (ignored is not null)
            (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, new HashSet<XpTrackedItem> { ignored }, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();
        if (situation == "module off")
            await this._bot.SetModuleAsync(Module.Leveling, false);

        this._bot.SendMessage(author, this.Guild.General, "hello");

        (await this.WaitForEarnedAsync(author, 1, NothingHappensWindow)).ShouldBeEmpty();
    }

    /// <summary>An ignored channel doesn't stop XP in other channels.</summary>
    /// <remarks>Cases: LVL-10.</remarks>
    [Fact]
    public async Task AnIgnoredChannel_DoesNotAffectOtherChannels()
    {
        (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, new HashSet<XpTrackedItem>
        {
            IgnoredChannel.Create(new ChannelId(this.Guild.ModLog.Id), this.GuildId, this.Owner, DateTimeOffset.UtcNow)
        }, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");

        (await this.WaitForEarnedAsync(this.Guild.User1, 1)).Count.ShouldBe(1);
    }

    // ── Levels and rewards ────────────────────────────────────────────────────

    /// <remarks>Cases: LVL-14.</remarks>
    [Fact]
    public async Task CrossingALevel_PostsToTheLevelingLog()
    {
        await this.SetAmountAsync(15);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        var embed = await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has leveled to level 2.");
        embed["footer"]!["text"]!.GetValue<string>().ShouldBe(this.Guild.User1.Id.ToString());
    }

    /// <remarks>Cases: LVL-15.</remarks>
    [Fact]
    public async Task StayingOnALevel_PostsNothing()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");
        await this.WaitForEarnedAsync(this.Guild.User1, 1);

        (await this.LoggedWithinAsync("has leveled", NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>Reaching a reward's level grants the role, DMs the reward message, and logs it.</summary>
    /// <remarks>Cases: LVL-30.</remarks>
    [Fact]
    public async Task ReachingARewardLevel_GrantsTheRole_DmsTheMessage_AndLogsIt()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, "well done");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has earned <@&{this.Guild.KickerRoleId}>");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(this.Guild.KickerRoleId);
        var dm = await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                                    && request.Path == $"channels/{this.Guild.User1.Id + 1000}/messages");
        dm.Json!["embeds"]![0]!["description"]!.GetValue<string>().ShouldBe("well done");
    }

    /// <summary>
    ///     X-4: only "\n" is turned into a line break. Any other backslash is kept: Regex.Unescape threw on \o/ and
    ///     lost the DM, and it stripped Markdown escapes.
    /// </summary>
    /// <remarks>Cases: LVL-30.</remarks>
    [Fact]
    public async Task ARewardMessageWithBackslashes_IsSentAsWritten_ExceptForLineBreaks()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, """well done \o/ \*really\* in C:\dir\nsee you""");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        var dm = await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                                    && request.Path == $"channels/{this.Guild.User1.Id + 1000}/messages");
        dm.Json!["embeds"]![0]!["description"]!.GetValue<string>().ShouldBe("well done \\o/ \\*really\\* in C:\\dir\nsee you");
    }

    /// <summary>A reward for a higher level isn't granted yet.</summary>
    /// <remarks>Cases: LVL-31.</remarks>
    [Fact]
    public async Task ARewardForAHigherLevel_IsNotGrantedYet()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 5, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");
        await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()?.EndsWith("has leveled to level 2.", StringComparison.Ordinal) == true);

        (await this.LoggedWithinAsync("has earned", NothingHappensWindow)).ShouldBeFalse();
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this.Guild.KickerRoleId);
    }

    /// <summary>A reward role above the bot's top role is reported as a setup problem.</summary>
    /// <remarks>Cases: LVL-37.</remarks>
    [Fact]
    public async Task ARewardRoleTheBotCannotGrant_IsReported()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.HigherRoleId, 2, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.StartsWith(
            $"I could not give the level reward roles <@&{this.Guild.HigherRoleId}>.", StringComparison.Ordinal) == true);
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this.Guild.HigherRoleId);
    }

    /// <summary>When the role couldn't be granted, the log shouldn't say the member earned it.</summary>
    /// <remarks>Cases: LVL-37.</remarks>
    [Fact]
    public async Task ARewardRoleTheBotCannotGrant_IsNotLoggedAsEarned()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.HigherRoleId, 2, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.StartsWith("I could not give", StringComparison.Ordinal) == true);

        (await this.LoggedWithinAsync("has earned", NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>
    ///     X-2: a reward whose role was deleted is removed, recorded as removed by the bot, and reported once as a
    ///     setup problem. That happens even when nobody has reached its level yet, and the other rewards are still
    ///     granted.
    /// </summary>
    /// <remarks>Cases: LVL-41.</remarks>
    [Fact]
    public async Task ARewardForADeletedRole_IsRemoved_TheModeratorsAreTold_AndTheOthersAreGranted()
    {
        await this.SetAmountAsync(15);
        var doomed = this._bot.Rest.CreateRole("doomed");
        await this.AddRewardAsync(doomed, 50, null);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, null);
        this._bot.Rest.DeleteRole(doomed);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        var notice = await this._bot.WaitForModLogEmbedAsync(IsRewardsRemovedIssue);
        notice["author"]!["name"]!.GetValue<string>().ShouldBe("Setup problem");
        notice["description"]!.GetValue<string>().ShouldStartWith(
            $"These level rewards were removed because their roles were deleted:\n**Level 50:** role `{doomed}`\n**Suggested fix:** ");
        await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has earned <@&{this.Guild.KickerRoleId}>");

        (await this.Settings.GetLevelingRewardsAsync(this.GuildId, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed()
            .Select(reward => reward.RoleId).ShouldBe([new RoleId(this.Guild.KickerRoleId)]);
        await using var settingsContext = database.CreateSettingsDbContext();
        (await settingsContext.Rewards.OfType<RewardRemoved>().SingleAsync(TestContext.Current.CancellationToken))
            .SetBy.ShouldBe(new ModeratorId(this.Guild.Bot.Id));
    }

    /// <summary>X-2: once removed, the reward for a deleted role isn't removed or reported again.</summary>
    /// <remarks>Cases: LVL-42.</remarks>
    [Fact]
    public async Task ARewardForADeletedRole_IsOnlyRemovedOnce()
    {
        await this.SetAmountAsync(15);
        var doomed = this._bot.Rest.CreateRole("doomed");
        await this.AddRewardAsync(doomed, 50, null);
        this._bot.Rest.DeleteRole(doomed);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "first");
        await this._bot.WaitForModLogEmbedAsync(IsRewardsRemovedIssue);
        this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "second");
        await this.WaitForEarnedAsync(this.Guild.Moderator, 1);
        await Task.Delay(NothingHappensWindow, TestContext.Current.CancellationToken);

        this._bot.Rest.Requests.Where(this._bot.IsModLogPost)
            .Count(request => request.Body?.Contains("level rewards were removed", StringComparison.Ordinal) == true).ShouldBe(1);
        await using var settingsContext = database.CreateSettingsDbContext();
        (await settingsContext.Rewards.OfType<RewardRemoved>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    /// <summary>
    ///     X-1: a member with a managed role, such as Server Booster, still gets their reward. Granting the whole role
    ///     list would include the managed role, which DSharpPlus refuses.
    /// </summary>
    /// <remarks>Cases: LVL-40.</remarks>
    [Fact]
    public async Task AMemberWithAManagedRole_StillEarnsTheReward()
    {
        ulong booster = 0;
        await this.StartAsync(guild =>
        {
            booster = guild.AddRole("Server Booster", managed: true);
            guild.GrantRole(guild.User1, booster);
        });
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has earned <@&{this.Guild.KickerRoleId}>");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldBe([booster, this.Guild.KickerRoleId], ignoreOrder: true);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>X-1: a reward role the bot can't grant is reported, and the other rewards are still granted.</summary>
    /// <remarks>Cases: LVL-39.</remarks>
    [Fact]
    public async Task ARewardTheBotCannotGrant_DoesNotBlockTheOthers()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.HigherRoleId, 2, null);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has earned <@&{this.Guild.KickerRoleId}>");
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.Contains(
            $"I could not give the level reward roles <@&{this.Guild.HigherRoleId}>.", StringComparison.Ordinal) == true);
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldBe([this.Guild.KickerRoleId]);
    }

    /// <summary>
    ///     X-2: setup problems are shown once a day per problem, but a removal is a different problem from the last one,
    ///     so a second removal on the same day is still shown.
    /// </summary>
    /// <remarks>Cases: LVL-42.</remarks>
    [Fact]
    public async Task ASecondRemovalOnTheSameDay_IsStillShown()
    {
        await this.SetAmountAsync(15);
        var first = this._bot.Rest.CreateRole("first");
        await this.AddRewardAsync(first, 50, null);
        this._bot.Rest.DeleteRole(first);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "first");
        await this._bot.WaitForModLogEmbedAsync(embed => IsRewardsRemovedIssue(embed)
                                                         && embed["description"]!.GetValue<string>().Contains($"`{first}`", StringComparison.Ordinal));

        var second = this._bot.Rest.CreateRole("second");
        await this.AddRewardAsync(second, 60, null);
        this._bot.Rest.DeleteRole(second);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "second");

        await this._bot.WaitForModLogEmbedAsync(embed => IsRewardsRemovedIssue(embed)
                                                         && embed["description"]!.GetValue<string>().Contains($"`{second}`", StringComparison.Ordinal));
    }

    private static bool IsRewardsRemovedIssue(JsonNode embed)
        => embed["description"]?.GetValue<string>().StartsWith("These level rewards were removed", StringComparison.Ordinal) == true;

    private async Task SetAmountAsync(int amount)
        => (await this.Settings.SetLevelingSettings(this.GuildId, this.Owner, LevelSettings.Amount, amount)).ShouldSucceed();

    private async Task AddRewardAsync(ulong roleId, int level, string? message)
        => (await this.Settings.SetRewardAsync(RewardAdded.Create(new RoleId(roleId), this.GuildId, level,
            message is null ? null : RewardMessage.Create(message).ShouldSucceed(), this.Owner, DateTimeOffset.UtcNow).ShouldSucceed())).ShouldSucceed();

    private Task<List<EarnedXp>> WaitForEarnedAsync(FakeUser user, int count, TimeSpan? window = null)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = database.CreateDbContext();
            return await dbContext.XpHistory.OfType<EarnedXp>()
                .Where(xp => xp.UserId == new UserId(user.Id) && xp.GuildId == this.GuildId)
                .ToListAsync(TestContext.Current.CancellationToken);
        }, earned => earned.Count >= count, window);

    private Task<bool> LoggedWithinAsync(string text, TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsLevelLogPost(request)
                                                               && request.Json?["embeds"]?[0]?["description"]?.ToString()
                                                                   .Contains(text, StringComparison.Ordinal) == true, window);
}

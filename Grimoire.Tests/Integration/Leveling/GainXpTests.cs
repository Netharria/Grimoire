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

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private ModeratorId Owner => new(this.Guild.Owner.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
        (await this.Settings.SetLogChannelSetting(GuildLogType.Leveling, this.GuildId, this.Owner,
            new ChannelId(this.Guild.ModLog.Id))).ShouldBeSetOrUnchanged();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Earning XP ────────────────────────────────────────────────────────────

    /// <summary>A message earns the configured amount, with a timeout that ends one timeout period later.</summary>
    [Fact]
    public async Task AMessage_EarnsXp_WithATimeout()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");

        var earned = (await this.WaitForEarnedAsync(this.Guild.User1, 1)).ShouldHaveSingleItem();
        earned.Xp.Value.ShouldBe(XpGainAmount.Default.Value);
        earned.TimeOut.ShouldBe(DateTimeOffset.UtcNow + XpTimeoutPeriod.Default.Value, TimeSpan.FromSeconds(30));
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASecondMessage_DuringTheTimeout_EarnsNothing()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "one");
        await this.WaitForEarnedAsync(this.Guild.User1, 1);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "two");

        (await this.WaitForEarnedAsync(this.Guild.User1, 2, NothingHappensWindow)).Count.ShouldBe(1);
    }

    /// <summary>Messages from bots, from ignored members, roles and channels, and with the module off, earn nothing.</summary>
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
            "ignored member" => IgnoredMember.Create(new UserId(author.Id), this.GuildId, this.Owner, now).ShouldSucceed(),
            "ignored role" => IgnoredRole.Create(new RoleId(this.Guild.ModeratorRoleId), this.GuildId, this.Owner, now).ShouldSucceed(),
            "ignored channel" => IgnoredChannel.Create(new ChannelId(this.Guild.General.Id), this.GuildId, this.Owner, now).ShouldSucceed(),
            _ => null
        };
        if (ignored is not null)
            (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, new HashSet<XpTrackedItem> { ignored })).ShouldSucceed();
        if (situation == "module off")
            await this._bot.SetModuleAsync(Module.Leveling, false);

        this._bot.SendMessage(author, this.Guild.General, "hello");

        (await this.WaitForEarnedAsync(author, 1, NothingHappensWindow)).ShouldBeEmpty();
    }

    /// <summary>An ignored channel doesn't stop XP in other channels.</summary>
    [Fact]
    public async Task AnIgnoredChannel_DoesNotAffectOtherChannels()
    {
        (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, new HashSet<XpTrackedItem>
        {
            IgnoredChannel.Create(new ChannelId(this.Guild.ModLog.Id), this.GuildId, this.Owner, DateTimeOffset.UtcNow).ShouldSucceed()
        })).ShouldSucceed();

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");

        (await this.WaitForEarnedAsync(this.Guild.User1, 1)).Count.ShouldBe(1);
    }

    // ── Levels and rewards ────────────────────────────────────────────────────

    [Fact]
    public async Task CrossingALevel_PostsToTheLevelingLog()
    {
        await this.SetAmountAsync(15);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        var embed = await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has leveled to level 2.");
        embed["footer"]!["text"]!.GetValue<string>().ShouldBe(this.Guild.User1.Id.ToString());
    }

    [Fact]
    public async Task StayingOnALevel_PostsNothing()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");
        await this.WaitForEarnedAsync(this.Guild.User1, 1);

        (await this.LoggedWithinAsync("has leveled", NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>Reaching a reward's level grants the role, DMs the reward message, and logs it.</summary>
    [Fact]
    public async Task ReachingARewardLevel_GrantsTheRole_DmsTheMessage_AndLogsIt()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, "well done");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has earned <@&{this.Guild.KickerRoleId}>");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(this.Guild.KickerRoleId);
        var dm = await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                                    && request.Path == $"channels/{this.Guild.User1.Id + 1000}/messages");
        dm.Json!["embeds"]![0]!["description"]!.GetValue<string>().ShouldBe("well done");
    }

    /// <summary>A reward for a higher level isn't granted yet.</summary>
    [Fact]
    public async Task ARewardForAHigherLevel_IsNotGrantedYet()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 5, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.EndsWith("has leveled to level 2.", StringComparison.Ordinal) == true);

        (await this.LoggedWithinAsync("has earned", NothingHappensWindow)).ShouldBeFalse();
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this.Guild.KickerRoleId);
    }

    /// <summary>A reward role above the bot's top role is reported as a setup problem.</summary>
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
    [Fact(Skip = "Fails: after Discord refuses the role change, GainUserXp still DMs the reward messages and logs " +
                 "\"has earned\" for roles the member never got. KI-41")]
    public async Task ARewardRoleTheBotCannotGrant_IsNotLoggedAsEarned()
    {
        await this.SetAmountAsync(15);
        await this.AddRewardAsync(this.Guild.HigherRoleId, 2, null);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.StartsWith("I could not give", StringComparison.Ordinal) == true);

        (await this.LoggedWithinAsync("has earned", NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>A reward whose role was deleted is reported, and the other rewards are still granted.</summary>
    [Fact]
    public async Task ARewardForADeletedRole_IsReported_AndTheOthersAreGranted()
    {
        await this.SetAmountAsync(15);
        var doomed = this._bot.Rest.CreateRole("doomed");
        await this.AddRewardAsync(doomed, 2, null);
        await this.AddRewardAsync(this.Guild.KickerRoleId, 2, null);
        this._bot.Rest.DeleteRole(doomed);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.StartsWith(
            $"Level rewards point at roles that no longer exist: `{doomed}`.", StringComparison.Ordinal) == true);
        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.Guild.GetMember(this.Guild.User1).RoleIds),
            roles => roles.Contains(this.Guild.KickerRoleId))).ShouldContain(this.Guild.KickerRoleId);
    }

    private async Task SetAmountAsync(int amount)
        => (await this.Settings.SetLevelingSettings(this.GuildId, this.Owner, LevelSettings.Amount, amount)).ShouldSucceed();

    private async Task AddRewardAsync(ulong roleId, int level, string? message)
        => (await this.Settings.SetRewardAsync(RewardAdded.Create(new RoleId(roleId), this.GuildId, level,
            message is null ? null : RewardMessage.Create(message).ShouldSucceed(), this.Owner, DateTimeOffset.UtcNow).ShouldSucceed())).ShouldSucceed();

    private Task<List<EarnedXp>> WaitForEarnedAsync(FakeUser user, int count, TimeSpan? window = null)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.XpHistory.OfType<EarnedXp>()
                .Where(xp => xp.UserId == new UserId(user.Id) && xp.GuildId == this.GuildId)
                .ToListAsync(TestContext.Current.CancellationToken);
        }, earned => earned.Count >= count, window);

    private Task<bool> LoggedWithinAsync(string text, TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                               && request.Json?["embeds"]?[0]?["description"]?.ToString()
                                                                   .Contains(text, StringComparison.Ordinal) == true, window);
}

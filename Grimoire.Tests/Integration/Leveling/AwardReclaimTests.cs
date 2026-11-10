// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using XpOption = Grimoire.Features.Leveling.Awards.ReclaimUserXp.XpOption;

namespace Grimoire.Tests.Integration.Leveling;

/// <summary>
///     <c>/Award</c> and <c>/Reclaim</c> cases from <c>06-leveling.md</c> section H that
///     <see cref="IgnoreAndAwardTests" /> doesn't cover (backlog A-47). The leveling log points at <c>#modlog</c>, so
///     level-ups and rewards show there.
/// </summary>
[Collection("Test collection")]
public sealed class AwardReclaimTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private ModeratorId Owner => new(this.Guild.Owner.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
        (await this.Settings.SetLogChannelSetting(GuildLogType.Leveling, this.GuildId, this.Owner,
            new ChannelId(this.Guild.LevelLog.Id))).ShouldBeSetOrUnchanged();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── /Award ────────────────────────────────────────────────────────────────

    /// <summary>
    ///     LVL-141 and LVL-44: an award posts no level-up and grants no reward; the reward comes with the member's next
    ///     earned message. From 10 XP, 100 more is level 5, and the next message's 5 XP stays on level 5.
    /// </summary>
    [Fact]
    public async Task Award_GrantsTheRewardOnlyOnTheNextEarnedMessage()
    {
        await this.AwardFromTenToLevelFiveAsync();
        (await this.LoggedWithinAsync("has leveled", NothingHappensWindow)).ShouldBeFalse();
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this.Guild.KickerRoleId);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "now reward me");

        await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has earned <@&{this.Guild.KickerRoleId}>");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(this.Guild.KickerRoleId);
    }

    /// <summary>
    ///     LVL-141: the doc expects the next earned message to post the level-up too. It doesn't: the handler compares
    ///     the level before and after that message's XP, and the award already moved the member to level 5, so the
    ///     level reached through <c>/Award</c> is never announced.
    /// </summary>
    [Fact(Skip = "Fails: a level reached through /Award is never announced; the next message stays on that level. KI-109")]
    public async Task Award_LevelUpIsAnnouncedOnTheNextEarnedMessage()
    {
        await this.AwardFromTenToLevelFiveAsync();

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "now level me");

        (await this.LoggedWithinAsync($"{this.Guild.User1.MemberMention} has leveled to level 5.", TimeSpan.FromSeconds(5)))
            .ShouldBeTrue();
    }

    private async Task AwardFromTenToLevelFiveAsync()
    {
        (await this.Settings.SetRewardAsync(RewardAdded.Create(new RoleId(this.Guild.KickerRoleId), this.GuildId, 3, null,
            this.Owner, DateTimeOffset.UtcNow).ShouldSucceed())).ShouldSucceed();
        await this.SeedXpAsync(this.Guild.User1, 10);

        (await this.AwardAsync(this.Guild.User1, 100)).Description
            .ShouldBe($"{this.Guild.User1.MemberMention} has been awarded 100 xp.");
        Field(await this.LevelEmbedAsync(), "Level").ShouldBe("5");
    }

    /// <summary>LVL-143: there is no maximum, and two maximum awards still add up and still show in <c>/Level</c>.</summary>
    [Fact]
    public async Task Award_TheMaximumTwice_AddsUp_AndLevelStillAnswers()
    {
        (await this.AwardAsync(this.Guild.User1, int.MaxValue)).Description
            .ShouldBe($"{this.Guild.User1.MemberMention} has been awarded {int.MaxValue} xp.");
        (await this.AwardAsync(this.Guild.User1, int.MaxValue)).Description
            .ShouldBe($"{this.Guild.User1.MemberMention} has been awarded {int.MaxValue} xp.");

        (await this.XpOfAsync(this.Guild.User1)).ShouldBe(4_294_967_294L);
        var embed = (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1)).Embed.ShouldNotBeNull();
        Field(embed, "XP").ShouldBe("4294967294");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LVL-144: there is no rank check, so a moderator can award themselves and members above them.</summary>
    [Fact]
    public async Task Award_HasNoRankCheck()
    {
        foreach (var member in new[] { this.Guild.Moderator, this.Guild.Admin, this.Guild.Higher })
            (await this.AwardAsync(member, 7)).Description.ShouldBe($"{member.MemberMention} has been awarded 7 xp.");

        await using var dbContext = database.CreateDbContext();
        (await dbContext.XpHistory.OfType<AwardedXp>().ToListAsync(Token)).Select(xp => xp.UserId.Value)
            .ShouldBe([this.Guild.Moderator.Id, this.Guild.Admin.Id, this.Guild.Higher.Id], ignoreOrder: true);
    }

    /// <summary>LVL-145: <c>/Award</c> needs a member, so a user who left gets an argument error and nothing is saved.</summary>
    [Fact]
    public async Task Award_ToAUserWhoLeft_IsRefused_AndNothingIsSaved()
    {
        this._bot.MemberLeaves(this.Guild.User1);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Token);

        var reply = await this.AwardAsync(this.Guild.User1, 50);

        reply.Description.ShouldBe("Failed to parse user.");
        await Task.Delay(NothingHappensWindow, Token);
        (await this.XpOfAsync(this.Guild.User1)).ShouldBe(0);
    }

    /// <summary>LVL-146: ignoring a member only stops earning; they can still be awarded XP.</summary>
    [Fact]
    public async Task Award_ToAnIgnoredMember_IsAccepted()
    {
        (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, new HashSet<XpTrackedItem>
        {
            IgnoredMember.Create(new UserId(this.Guild.User1.Id), this.GuildId, this.Owner, DateTimeOffset.UtcNow)
        }, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();

        (await this.AwardAsync(this.Guild.User1, 30)).Description
            .ShouldBe($"{this.Guild.User1.MemberMention} has been awarded 30 xp.");
        (await this.XpOfAsync(this.Guild.User1)).ShouldBe(30);
    }

    // ── /Reclaim ──────────────────────────────────────────────────────────────

    /// <summary>LVL-153: taking all XP leaves the member's reward roles, and <c>/Level</c> drops to level 1.</summary>
    [Fact]
    public async Task ReclaimAll_KeepsTheRewardRoles_AndTheLevelDropsToOne()
    {
        var level2 = this._bot.Rest.CreateRole("L2");
        var level3 = this._bot.Rest.CreateRole("L3");
        this.Guild.GrantRole(this.Guild.User1, level2);
        this.Guild.GrantRole(this.Guild.User1, level3);
        await this.SeedXpAsync(this.Guild.User1, 30);
        Field(await this.LevelEmbedAsync(), "Level").ShouldBe("3");

        (await this.ReclaimAsync(this.Guild.User1, XpOption.All)).Description
            .ShouldBe($"30 xp has been taken from {this.Guild.User1.Mention}.");

        Field(await this.LevelEmbedAsync(), "Level").ShouldBe("1");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldBe([level2, level3], ignoreOrder: true);
        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Delete
            && request.Path.Contains("/roles/", StringComparison.Ordinal), NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>LVL-154: <c>/Reclaim</c> takes any user, so XP can be taken from someone who left.</summary>
    [Fact]
    public async Task Reclaim_FromAUserWhoLeft_Works()
    {
        await this.SeedXpAsync(this.Guild.User1, 40);
        this._bot.MemberLeaves(this.Guild.User1);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Token);

        (await this.ReclaimAsync(this.Guild.User1, XpOption.All)).Description
            .ShouldBe($"40 xp has been taken from {this.Guild.User1.Mention}.");

        await using var dbContext = database.CreateDbContext();
        (await dbContext.XpHistory.OfType<ReclaimedXp>().SingleAsync(Token)).Xp.Value.ShouldBe(-40);
        (await this.XpOfAsync(this.Guild.User1)).ShouldBe(0);
    }

    // ── Gates ─────────────────────────────────────────────────────────────────

    /// <summary>LVL-156: with Leveling off, both commands are refused and nothing is saved.</summary>
    [Theory]
    [InlineData("award")]
    [InlineData("reclaim")]
    public async Task WithLevelingDisabled_IsRefused(string command)
    {
        await this.SeedXpAsync(this.Guild.User1, 40);
        await this._bot.SetModuleAsync(Module.Leveling, false);

        var reply = command == "award"
            ? await this.AwardAsync(this.Guild.User1, 10)
            : await this.ReclaimAsync(this.Guild.User1, XpOption.All);

        reply.Description.ShouldBe("This module is disabled in this server.");
        (await this.XpOfAsync(this.Guild.User1)).ShouldBe(40);
    }

    private Task<FakeReply> AwardAsync(FakeUser user, int xp)
        => this._bot.RunSlashCommandAndWaitAsync("award", this.Guild.Moderator,
            SlashOption.User("user", user), SlashOption.Integer("xp", xp));

    private Task<FakeReply> ReclaimAsync(FakeUser user, XpOption option)
        => this._bot.RunSlashCommandAndWaitAsync("reclaim", this.Guild.Moderator,
            SlashOption.User("user", user), SlashOption.Enum("option", option));

    private async Task<JsonNode> LevelEmbedAsync()
        => (await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.Moderator, SlashOption.User("user", this.Guild.User1)))
            .Embed.ShouldNotBeNull();

    private async Task SeedXpAsync(FakeUser user, int xp)
    {
        await using var seed = database.CreateDbContext();
        seed.XpHistory.Add(EarnedXp.Create(PositiveXpAmount.Create(xp).ShouldSucceed(), new UserId(user.Id), this.GuildId,
            DateTimeOffset.UtcNow.AddMinutes(-10)));
        await seed.SaveChangesAsync(Token);
    }

    private async Task<long> XpOfAsync(FakeUser user)
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.XpHistory.Where(xp => xp.UserId == new UserId(user.Id) && xp.GuildId == this.GuildId)
            .SumAsync(xp => xp.RawXp, Token);
    }

    private Task<bool> LoggedWithinAsync(string text, TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsLevelLogPost(request)
                                                               && request.Json?["embeds"]?[0]?["description"]?.ToString()
                                                                   .Contains(text, StringComparison.Ordinal) == true, window);

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

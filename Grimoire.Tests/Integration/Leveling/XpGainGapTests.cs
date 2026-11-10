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
///     <c>GainUserXp</c> cases from <c>06-leveling.md</c> section A that <see cref="GainXpTests" /> doesn't cover
///     (backlog A-46): timeouts ending, reply and system messages, several levels at once, the leveling log off,
///     separate members and guilds, webhooks, and ignored channels with threads or categories (KI-73). An ended timeout
///     is simulated by seeding or backdating the newest row, since the shortest timeout is a minute.
/// </summary>
[Collection("Test collection")]
public sealed class XpGainGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private ModeratorId Owner => new(this.Guild.Owner.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        await this.StartAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>Starts the bot, or restarts it on the same database, with the leveling log at <c>#modlog</c>.</summary>
    private async Task StartAsync(bool secondGuild = false)
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, secondGuild: secondGuild);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Leveling, true);
        (await this.Settings.SetLogChannelSetting(GuildLogType.Leveling, this.GuildId, this.Owner,
            new ChannelId(this.Guild.LevelLog.Id))).ShouldBeSetOrUnchanged();
    }

    // ── Timeouts ──────────────────────────────────────────────────────────────

    /// <summary>LVL-03: once the newest row's timeout has passed, the next message earns again.</summary>
    [Fact]
    public async Task AMessage_AfterTheTimeoutHasPassed_EarnsAgain()
    {
        await this.SeedEarnedAsync(this.Guild.User1, 5, DateTimeOffset.UtcNow.AddSeconds(-1));

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "again");

        (await this.WaitForEarnedAsync(this.Guild.User1, 2)).Count.ShouldBe(2);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     LVL-04: with the timeout set to one minute, a row's timeout is a minute after it was earned. Messages in that
    ///     minute earn nothing, and the first message after it earns the second row.
    /// </summary>
    [Fact]
    public async Task AOneMinuteTimeout_LetsTheMemberEarnAgainAfterAMinute()
    {
        (await this.Settings.SetLevelingSettings(this.GuildId, this.Owner, LevelSettings.XpTimeoutPeriod, 1, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "one");
        var first = (await this.WaitForEarnedAsync(this.Guild.User1, 1)).ShouldHaveSingleItem();
        first.TimeOut.ShouldBe(DateTimeOffset.UtcNow.AddMinutes(1), TimeSpan.FromSeconds(15));
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "too soon");
        (await this.WaitForEarnedAsync(this.Guild.User1, 2, NothingHappensWindow)).Count.ShouldBe(1);

        await this.BackdateTimeoutsAsync(this.Guild.User1, TimeSpan.FromMinutes(1));
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "a minute later");

        (await this.WaitForEarnedAsync(this.Guild.User1, 2)).Count.ShouldBe(2);
    }

    // ── Message kinds ─────────────────────────────────────────────────────────

    /// <summary>LVL-06: a reply counts like an ordinary message.</summary>
    [Fact]
    public async Task AReply_EarnsXp()
    {
        var original = this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "question");

        this.DispatchMessage(this.Guild.User1, this.Guild.General, "answer", 19, original);

        (await this.WaitForEarnedAsync(this.Guild.User1, 1)).Count.ShouldBe(1);
    }

    /// <summary>
    ///     LVL-07: a system message, such as "pinned a message" (type 6), earns nothing, and neither does running a
    ///     slash command, which isn't a message at all.
    /// </summary>
    [Fact]
    public async Task ASystemMessageOrASlashCommand_EarnsNothing()
    {
        this.DispatchMessage(this.Guild.User1, this.Guild.General, string.Empty, 6);
        await this._bot.RunSlashCommandAndWaitAsync("level", this.Guild.User1);

        (await this.WaitForEarnedAsync(this.Guild.User1, 1, NothingHappensWindow)).ShouldBeEmpty();

        // The same member's ordinary message still earns, so the check above isn't passing on a broken handler.
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");
        (await this.WaitForEarnedAsync(this.Guild.User1, 1)).Count.ShouldBe(1);
    }

    /// <summary>
    ///     LVL-20 without PluralKit: the proxied copy is posted by a webhook, which has no member, so it earns
    ///     nothing. The member's own message earns once. A-50 covers the PluralKit lookup itself.
    /// </summary>
    [Fact]
    public async Task AWebhookCopyOfAMessage_EarnsNothing_TheOriginalEarnsOnce()
    {
        var proxy = new FakeUser(1_100_000_000_000_009_001, "proxied member");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello from my system");
        this._bot.SendMessage(proxy, this.Guild.General, "hello from my system");

        (await this.WaitForEarnedAsync(this.Guild.User1, 1)).Count.ShouldBe(1);
        (await this.WaitForEarnedAsync(proxy, 1, NothingHappensWindow)).ShouldBeEmpty();
    }

    // ── Levels and the log ────────────────────────────────────────────────────

    /// <summary>LVL-16: one message that jumps from level 1 to level 5 posts one level-up, for level 5 only.</summary>
    [Fact]
    public async Task JumpingSeveralLevels_PostsOneLevelUp_ForTheNewLevel()
    {
        await this.SetAmountAsync(100);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "big jump");

        await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.User1.MemberMention} has leveled to level 5.");
        await Task.Delay(NothingHappensWindow, Token);
        this.LevelUpPosts().Count().ShouldBe(1);
        (await this.WaitForEarnedAsync(this.Guild.User1, 1)).ShouldHaveSingleItem().Xp.Value.ShouldBe(100);
    }

    /// <summary>LVL-17: with the leveling log off, a level-up is posted nowhere, and the XP is still saved.</summary>
    [Fact]
    public async Task WithTheLevelingLogOff_ALevelUpIsPostedNowhere_ButTheXpIsSaved()
    {
        (await this.Settings.SetLogChannelSetting(GuildLogType.Leveling, this.GuildId, this.Owner, null, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();
        await this.SetAmountAsync(15);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "level me");

        (await this.WaitForEarnedAsync(this.Guild.User1, 1)).Count.ShouldBe(1);
        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Post
                                                                   && request.Body?.Contains("has leveled", StringComparison.Ordinal) == true,
            NothingHappensWindow)).ShouldBeFalse();
    }

    // ── Separate members and guilds ───────────────────────────────────────────

    /// <summary>LVL-18: two members earn separately: one member's timeout doesn't hold back the other.</summary>
    [Fact]
    public async Task TwoMembers_EachEarnTheirOwnXp()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "me");
        (await this.WaitForEarnedAsync(this.Guild.User1, 1)).Count.ShouldBe(1);

        this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "me too");

        (await this.WaitForEarnedAsync(this.Guild.Moderator, 1)).Count.ShouldBe(1);
    }

    /// <summary>
    ///     LVL-21: XP is kept per guild. A member still in their timeout in one guild earns in another, and the first
    ///     guild's total doesn't change.
    /// </summary>
    [Fact]
    public async Task XpInOneGuild_IsSeparateFromAnother()
    {
        await this.StartAsync(secondGuild: true);
        var second = this._bot.SecondGuild!;
        await this._bot.SetModuleAsync(Module.Leveling, true, second);

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "in A");
        await this.WaitForEarnedAsync(this.Guild.User1, 1);
        this._bot.SendMessage(second.User1, second.General, "in B");

        (await this.WaitForEarnedAsync(second.User1, 1, guildId: new GuildId(second.Id))).Count.ShouldBe(1);
        (await this.WaitForEarnedAsync(this.Guild.User1, 2, NothingHappensWindow)).Sum(xp => xp.Xp.Value)
            .ShouldBe(XpGainAmount.Default.Value);
    }

    // ── Ignored channels (KI-73) ──────────────────────────────────────────────

    /// <summary>LVL-11, review X-12: a thread under an ignored channel is ignored too.</summary>
    [Fact]
    public async Task AThreadUnderAnIgnoredChannel_EarnsNothing()
    {
        await this.IgnoreChannelAsync(this.Guild.General);

        this._bot.SendMessage(this.Guild.User1, this.Guild.PublicThread, "in the thread");

        (await this.WaitForEarnedAsync(this.Guild.User1, 1, NothingHappensWindow)).ShouldBeEmpty();
    }

    /// <summary>
    ///     LVL-131 (and LVL-12): <c>/Ignore Add</c> accepts a category with the usual "now ignored" reply, but ignoring
    ///     it does nothing for the channels in it. It should refuse the category or say so. The fake guild has no
    ///     channel inside a category, so LVL-12's XP check stays manual.
    /// </summary>
    [Fact(Skip = "Fails: /Ignore Add accepts a category as if it worked, though a category doesn't cover its channels. KI-73")]
    public async Task IgnoringACategory_IsRefusedOrWarned()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("ignore", this.Guild.Admin,
            SlashOption.Subcommand("add", SlashOption.Channel("channel", this.Guild.Category)));

        reply.Description.ShouldNotBe($"{this.Guild.Category.Mention} are now ignored for xp gain.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Sends a <c>MESSAGE_CREATE</c> of a given Discord message type, such as 19 (reply) or 6 (pinned message),
    ///     which <see cref="GrimoireTestBot.SendMessage" /> can't.
    /// </summary>
    private void DispatchMessage(FakeUser author, FakeChannel channel, string content, int type, ulong? replyTo = null)
        => this._bot.Gateway.Dispatch("MESSAGE_CREATE", new JsonObject
        {
            ["id"] = FakeUser.Snowflake(this._bot.NextSnowflake()),
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["author"] = author.ToJson(),
            ["member"] = FakeGuild.MemberJson(this.Guild.GetMember(author), false),
            ["content"] = content,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["edited_timestamp"] = null,
            ["tts"] = false,
            ["mention_everyone"] = false,
            ["mentions"] = new JsonArray(),
            ["mention_roles"] = new JsonArray(),
            ["attachments"] = new JsonArray(),
            ["embeds"] = new JsonArray(),
            ["components"] = new JsonArray(),
            ["pinned"] = false,
            ["type"] = type,
            ["flags"] = 0,
            ["message_reference"] = replyTo is { } id
                ? new JsonObject
                {
                    ["message_id"] = FakeUser.Snowflake(id),
                    ["channel_id"] = FakeUser.Snowflake(channel.Id),
                    ["guild_id"] = FakeUser.Snowflake(this.Guild.Id)
                }
                : null
        });

    private IEnumerable<RecordedRequest> LevelUpPosts()
        => this._bot.Rest.Requests.Where(request => this._bot.IsLevelLogPost(request)
                                                    && request.Body?.Contains("has leveled", StringComparison.Ordinal) == true);

    private async Task SetAmountAsync(int amount)
        => (await this.Settings.SetLevelingSettings(this.GuildId, this.Owner, LevelSettings.Amount, amount)).ShouldSucceed();

    private async Task IgnoreChannelAsync(FakeChannel channel)
        => (await this.Settings.AppendIgnoredItemsEvent(this.GuildId, new HashSet<XpTrackedItem>
        {
            IgnoredChannel.Create(new ChannelId(channel.Id), this.GuildId, this.Owner, DateTimeOffset.UtcNow).ShouldSucceed()
        })).ShouldSucceed();

    private async Task SeedEarnedAsync(FakeUser user, int xp, DateTimeOffset timeOut)
    {
        await using var seed = this._database.CreateDbContext();
        seed.XpHistory.Add(EarnedXp.Create(PositiveXpAmount.Create(xp).ShouldSucceed(), new UserId(user.Id), this.GuildId, timeOut));
        await seed.SaveChangesAsync(Token);
    }

    /// <summary>Moves the member's timeouts back, as if <paramref name="by" /> had passed. The timeout is part of the key.</summary>
    private async Task BackdateTimeoutsAsync(FakeUser user, TimeSpan by)
    {
        await using var dbContext = this._database.CreateDbContext();
        await dbContext.Database.ExecuteSqlAsync(
            $"""UPDATE "XpHistory" SET "TimeOut" = "TimeOut" - {by} WHERE "UserId" = {(decimal)user.Id} AND "GuildId" = {(decimal)this.Guild.Id}""",
            Token);
    }

    private Task<List<EarnedXp>> WaitForEarnedAsync(FakeUser user, int count, TimeSpan? window = null, GuildId? guildId = null)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            var guild = guildId ?? this.GuildId;
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.XpHistory.OfType<EarnedXp>()
                .Where(xp => xp.UserId == new UserId(user.Id) && xp.GuildId == guild)
                .ToListAsync(Token);
        }, earned => earned.Count >= count, window);
}

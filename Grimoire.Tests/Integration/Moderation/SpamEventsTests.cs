// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json.Nodes;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     The spam filter's events (<c>SpamEvents</c>, <c>SpamTrackerModule</c>), cases SPAM-03 to SPAM-34 in
///     <c>docs/testing/04-spamfilter-diagnostics.md</c> section A, and XC-83. The cases already in
///     <c>SpamFilterTests</c> are not repeated. SPAM-14 needs channel-resolved permissions (H-13), and SPAM-31 is
///     PluralKit's, so neither is here.
/// </summary>
[Collection("Test collection")]
public sealed class SpamEventsTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string SpamReason = "Auto mod: Spam detected.";
    private static readonly TimeSpan HandlersSettle = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BetweenMessages = TimeSpan.FromMilliseconds(50);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;
    private ulong _muteRoleId;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private ModeratorId OwnerId => new(this.Guild.Owner.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        await this.StartBotAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Escalation ────────────────────────────────────────────────────────────

    /// <summary>
    ///     SPAM-03, SPAM-04: each spam mute in the last day doubles the next, 1 then 2 then 4 minutes. Between bursts the
    ///     mute is lifted and the role taken off, as the expiry task would.
    /// </summary>
    [Fact]
    public async Task RepeatedSpam_DoublesTheMuteEachTime()
    {
        foreach (var minutes in new[] { 1, 2, 4 })
        {
            var started = DateTimeOffset.UtcNow;
            await this.SpamAsync(this.Guild.User1, this.Guild.General);

            var embed = await this.WaitForSpamMuteLogAsync(minutes);
            Field(embed, "Reason").ShouldBe(SpamReason);
            var mute = await this.ActiveMuteAsync(this.Guild.User1);
            (mute.EndTime - started).ShouldBeInRange(TimeSpan.FromMinutes(minutes), TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(10));

            await this.LiftMuteAsync(this.Guild.User1);
        }

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.CountAsync(sin => sin.SinType == SinType.Mute, TestContext.Current.CancellationToken)).ShouldBe(3);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>SPAM-05: a manual mute from an hour ago counts toward the escalation (KI-63, to decide).</summary>
    [Fact]
    public async Task AManualMuteEarlierToday_DoublesTheSpamMute()
    {
        await this.SeedMuteSinAsync(new ModerationActor.Moderator(new ModeratorId(this.Guild.Moderator.Id)), DateTimeOffset.UtcNow.AddHours(-1));

        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this.WaitForSpamMuteLogAsync(2);
    }

    /// <summary>SPAM-06: a pardoned spam mute still counts toward the escalation (KI-63, to decide).</summary>
    [Fact]
    public async Task APardonedSpamMute_StillDoublesTheNext()
    {
        var sin = await this.SeedMuteSinAsync(new ModerationActor.System(), DateTimeOffset.UtcNow.AddMinutes(-30));
        await using (var seed = this._database.CreateDbContext())
        {
            seed.Pardons.Add(new Pardon
            {
                SinId = sin.Id,
                GuildId = this.GuildId,
                ModeratorId = new ModeratorId(this.Guild.Admin.Id),
                Reason = ModerationReason.Create("false positive").ShouldSucceed(),
                SetAt = DateTimeOffset.UtcNow.AddMinutes(-20)
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this.WaitForSpamMuteLogAsync(2);
    }

    /// <summary>A mute older than a day no longer counts, so the next spam mute is back to one minute.</summary>
    [Fact]
    public async Task AMuteOlderThanADay_DoesNotCount()
    {
        await this.SeedMuteSinAsync(new ModerationActor.System(), DateTimeOffset.UtcNow.AddHours(-25));

        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this.WaitForSpamMuteLogAsync(1);
    }

    /// <summary>
    ///     SPAM-34: a muted member who can still post somewhere (a leaky channel) is muted again while the first mute is
    ///     active. The second mute doubles, and both sins and both mutes are recorded.
    /// </summary>
    [Fact]
    public async Task SpamWhileStillMuted_MutesAgain_ForTwiceAsLong()
    {
        await this.SpamAsync(this.Guild.User1, this.Guild.General);
        await this.WaitForSpamMuteLogAsync(1);

        await this.SpamAsync(this.Guild.User1, this.Guild.Announcements);
        await this.WaitForSpamMuteLogAsync(2);

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.OfType<MuteAdded>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    /// <summary>SPAM-09: three different messages that each mention six members, about 25 points each, trip on the third.</summary>
    [Fact]
    public async Task MessagesFullOfMentions_AreSpamOnTheThird()
    {
        FakeUser[] mentioned = [this.Guild.Owner, this.Guild.Moderator, this.Guild.Admin, this.Guild.Higher, this.Guild.OtherBot, this.Guild.Bot];
        var mentions = string.Join(' ', mentioned.Select(user => user.Mention));
        var ids = new List<ulong>();
        foreach (var text in new[] { "look at this", "and this one", "seriously everyone" })
        {
            ids.Add(this._bot.SendMessageMentioning(this.Guild.User1, this.Guild.General, $"{text} {mentions}", mentioned));
            await Task.Delay(BetweenMessages, TestContext.Current.CancellationToken);
        }

        await this.ShouldBeSpamAtAsync(ids, 2);
    }

    /// <summary>SPAM-10: six one-word messages are exactly 60 points, which is not over the threshold. The seventh is.</summary>
    [Fact]
    public async Task SevenQuickOneWordMessages_AreSpamOnTheSeventh()
    {
        var ids = new List<ulong>();
        foreach (var word in new[] { "a", "b", "c", "d", "e", "f", "g" })
        {
            ids.Add(this._bot.SendMessage(this.Guild.User1, this.Guild.General, word));
            await Task.Delay(BetweenMessages, TestContext.Current.CancellationToken);
        }

        await this.ShouldBeSpamAtAsync(ids, 6);
    }

    /// <summary>SPAM-11: a 40-line message is about 39 points, and repeating it at once is spam.</summary>
    [Fact]
    public async Task ALongMessageRepeated_IsSpamOnTheSecondCopy()
    {
        var text = string.Join('\n', Enumerable.Range(1, 40).Select(line => $"line {line}"));
        var first = this._bot.SendMessage(this.Guild.User1, this.Guild.General, text);
        await Task.Delay(BetweenMessages, TestContext.Current.CancellationToken);
        var second = this._bot.SendMessage(this.Guild.User1, this.Guild.General, text);

        await this.ShouldBeSpamAtAsync([first, second], 1);
    }

    // ── Who is filtered ───────────────────────────────────────────────────────

    /// <summary>
    ///     SPAM-13 and SPAM-30: only Manage Channels or ownership exempts. <c>mod</c> (Manage Messages) and <c>higher</c>
    ///     (ranked above the bot) are muted like anyone, because Discord only checks that the mute role is below the bot.
    /// </summary>
    [Theory]
    [InlineData("mod")]
    [InlineData("higher")]
    public async Task MembersWithoutManageChannels_AreFiltered(string who)
    {
        var member = who == "mod" ? this.Guild.Moderator : this.Guild.Higher;

        await this.SpamAsync(member, this.Guild.General);

        await this.WaitForSpamMuteLogAsync(1);
        this.Guild.GetMember(member).RoleIds.ShouldContain(this._muteRoleId);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.SingleAsync(TestContext.Current.CancellationToken)).UserId.ShouldBe(new UserId(member.Id));
    }

    /// <summary>SPAM-24: a voice channel's text chat is filtered like any channel.</summary>
    [Fact]
    public async Task SpamInAVoiceChannelsChat_IsFiltered()
    {
        await this.SpamAsync(this.Guild.User1, this.Guild.Voice);

        await this.WaitForSpamMuteLogAsync(1);
        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Delete
                                                            && request.Path.StartsWith($"channels/{this.Guild.Voice.Id}/messages/", StringComparison.Ordinal));
    }

    // ── Overrides up the channel tree ─────────────────────────────────────────

    /// <summary>
    ///     SPAM-20 to SPAM-23: the walk goes thread, channel, category. The first Always filters, the first Never stops
    ///     filtering, and Inherit (or nothing) moves up.
    /// </summary>
    [Theory]
    [InlineData("SPAM-20", "Never", null, null, false)]
    [InlineData("SPAM-21", "Never", "Always", null, true)]
    [InlineData("SPAM-22", "Never", "Inherit", null, false)]
    [InlineData("SPAM-23", null, "Never", "Always", true)]
    public async Task Overrides_AreInheritedFromTheNearestSetting(string caseId, string? category, string? channel, string? thread,
        bool filtered)
    {
        await this.RestartWithGeneralInTheCategoryAsync();
        await this.OverrideAsync(this.Guild.Category, category);
        await this.OverrideAsync(this.Guild.General, channel);
        await this.OverrideAsync(this.Guild.PublicThread, thread);

        await this.SpamAsync(this.Guild.User1, thread is null ? this.Guild.General : this.Guild.PublicThread);

        if (filtered)
            await this.WaitForSpamMuteLogAsync(1);
        else
            await this.ShouldNotBeFilteredAsync();
        this._bot.Logs.Errors.ShouldBeEmpty(caseId);
    }

    // ── Telling people ────────────────────────────────────────────────────────

    /// <summary>SPAM-26: with DMs closed the member is still muted, and the mod log is told the DM failed (KI-16).</summary>
    [Fact]
    public async Task SpamFromAMemberWithClosedDms_IsStillMuted_AndTheModLogTold()
    {
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Guild.User1.Id + 1_000}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50007, "Cannot send messages to this user"));

        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this.WaitForSpamMuteLogAsync(1);
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"Was not able to send a direct message with the mute details to {this.Guild.User1.MemberMention}");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(this._muteRoleId);
        await this.ActiveMuteAsync(this.Guild.User1);
    }

    // ── When the mute can't be given (KI-55) ──────────────────────────────────

    /// <summary>
    ///     SPAM-27, SPAM-28, SPAM-29: when the bot can't give the mute role (no Manage Roles, the role above the bot, or
    ///     the role deleted), nothing should be recorded. Today the sin and the mute are saved first, so the expiry
    ///     task later "unmutes" a member who was never muted.
    /// </summary>
    [Theory(Skip = "Fails: the sin and MuteAdded row are saved before the role is looked up or granted. KI-55")]
    [InlineData("SPAM-27 no Manage Roles")]
    [InlineData("SPAM-28 role above the bot")]
    [InlineData("SPAM-29 role deleted")]
    public async Task WhenTheMuteRoleCannotBeGiven_NothingIsRecorded(string situation)
    {
        if (situation.StartsWith("SPAM-27", StringComparison.Ordinal))
            await this.RestartAsync(guild => guild.BotPermissions = FakeGuild.ManageMessagesPermission | FakeGuild.ViewChannelPermission
                                                                    | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        else if (situation.StartsWith("SPAM-28", StringComparison.Ordinal))
            await this.UseMuteRoleAsync(this._bot.Rest.CreateRole("Muted above", position: FakeGuild.RoleAboveTheBot));
        else
        {
            this._bot.Rest.DeleteRole(this._muteRoleId);
            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }

        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await Task.Delay(HandlersSettle, TestContext.Current.CancellationToken);
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this._muteRoleId);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse(situation);
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse(situation);
    }

    // ── Where points live ─────────────────────────────────────────────────────

    /// <summary>
    ///     SPAM-32: points are kept per guild. Five quick messages in each of two guilds are 50 points in each, not 100
    ///     in one. The second guild is set up to mute, so spam there would show.
    /// </summary>
    [Fact]
    public async Task Points_AreKeptPerGuild()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, secondGuild: true);
        await this.ConfigureSpamFilterAsync(this.Guild);
        var second = this._bot.SecondGuild!;
        await this.ConfigureSpamFilterAsync(second);

        await this.SendDifferentMessagesAsync(this.Guild.General, "one", "two", "three", "four", "five");
        await this.SendDifferentMessagesAsync(second.General, "six", "seven", "eight", "nine", "ten");

        await this.ShouldNotBeFilteredAsync();
    }

    /// <summary>
    ///     SPAM-33: points live in memory, so a restart forgets them. Five quick messages before and five after are not
    ///     spam, though ten in a row would be.
    /// </summary>
    [Fact]
    public async Task Points_AreForgottenOnRestart()
    {
        await this.SendDifferentMessagesAsync(this.Guild.General, "one", "two", "three", "four", "five");
        await Task.Delay(HandlersSettle, TestContext.Current.CancellationToken);

        await this.RestartAsync();
        await this.SendDifferentMessagesAsync(this.Guild.General, "six", "seven", "eight", "nine", "ten");

        await this.ShouldNotBeFilteredAsync();
    }

    // ── Modules ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     XC-83: AntiSpam on and Moderation off, with a mute role set. There is no effective mute role, so nothing
    ///     happens, and the spam is not deleted either (KI-107, to decide).
    /// </summary>
    [Fact]
    public async Task WithModerationOff_SpamIsNeitherMutedNorDeleted()
    {
        await this._bot.SetModuleAsync(Module.Moderation, false);

        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this.ShouldNotBeFilteredAsync();
        this._bot.Rest.Requests.ShouldNotContain(request => request.Method == HttpMethod.Delete && request.Path.Contains("/messages/", StringComparison.Ordinal));
        this._bot.Rest.Requests.ShouldNotContain(request => this._bot.IsModLogPost(request));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task StartBotAsync(Action<FakeGuild>? configureGuild = null)
    {
        this._bot = await GrimoireTestBot.StartAsync(this._database, configureGuild);
        await this.ConfigureSpamFilterAsync(this.Guild);
    }

    /// <summary>A restart keeps the database. The new fake guild hands out the same id for the recreated mute role.</summary>
    private async Task RestartAsync(Action<FakeGuild>? configureGuild = null)
    {
        await this._bot.DisposeAsync();
        await this.StartBotAsync(configureGuild);
    }

    private Task RestartWithGeneralInTheCategoryAsync()
        => this.RestartAsync(guild => guild.SetParent(guild.General, guild.Category));

    /// <summary>Moderation on with the mod log, AntiSpam on, and a Muted role the bot can give.</summary>
    private async Task ConfigureSpamFilterAsync(FakeGuild guild)
    {
        await this._bot.ConfigureModerationAsync(guild);
        await this._bot.SetModuleAsync(Module.AntiSpam, true, guild);
        var roleId = guild.AddRole("Muted");
        this._bot.Gateway.Dispatch("GUILD_ROLE_CREATE", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(guild.Id), ["role"] = guild.RoleJsonOf(roleId)!.DeepClone()
        });
        (await this.Settings.SetMuteRole(new GuildId(guild.Id), new ModeratorId(guild.Owner.Id), new RoleId(roleId))).ShouldBeSetOrUnchanged();
        if (guild == this.Guild)
            this._muteRoleId = roleId;
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
    }

    private async Task UseMuteRoleAsync(ulong roleId)
    {
        (await this.Settings.SetMuteRole(this.GuildId, this.OwnerId, new RoleId(roleId))).ShouldSucceed();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
    }

    private async Task OverrideAsync(FakeChannel channel, string? option)
    {
        if (option is null)
            return;
        var setting = option switch
        {
            "Always" => SpamFilterOverrideOption.AlwaysFilter,
            "Never" => SpamFilterOverrideOption.NeverFilter,
            _ => SpamFilterOverrideOption.Inherit
        };
        (await this.Settings.SetSpamFilterOverrideAsync(new ChannelId(channel.Id), this.GuildId, this.OwnerId, setting,
            TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();
    }

    /// <summary>
    ///     Four identical messages in a row: about 10, 30, 50 and 70 points, so the fourth (or, straight after an earlier
    ///     burst whose text it repeats, the third) trips the filter. The points then reset, so one burst is one mute.
    /// </summary>
    private async Task SpamAsync(FakeUser user, FakeChannel channel)
    {
        for (var i = 0; i < 4; i++)
        {
            this._bot.SendMessage(user, channel, "buy cheap gold now");
            await Task.Delay(BetweenMessages, TestContext.Current.CancellationToken);
        }
    }

    private async Task SendDifferentMessagesAsync(FakeChannel channel, params string[] texts)
    {
        foreach (var text in texts)
        {
            this._bot.SendMessage(this.Guild.User1, channel, text);
            await Task.Delay(BetweenMessages, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Exactly the message at <paramref name="spamIndex" /> tripped the filter: it alone is deleted, with one sin.</summary>
    private async Task ShouldBeSpamAtAsync(IReadOnlyList<ulong> ids, int spamIndex)
    {
        await this.WaitForSpamMuteLogAsync(1);
        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Delete
                                                            && request.Path == $"channels/{this.Guild.General.Id}/messages/{ids[spamIndex]}");
        await Task.Delay(HandlersSettle, TestContext.Current.CancellationToken);
        this._bot.Rest.Requests.Where(request => request.Method == HttpMethod.Delete && request.Path.Contains("/messages/", StringComparison.Ordinal))
            .ShouldHaveSingleItem();
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    private Task<JsonNode> WaitForSpamMuteLogAsync(int minutes)
        => this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Mute"
                                                  && Field(e, "Length") == $"{minutes} minute(s)");

    private static string? Field(JsonNode embed, string name)
        => embed["fields"]?.AsArray().FirstOrDefault(field => field?["name"]?.GetValue<string>() == name)?["value"]?.GetValue<string>();

    private async Task<MuteAdded> ActiveMuteAsync(FakeUser user)
        => await GrimoireTestBot.EventuallyAsync(
            async () => (await this.Settings.GetActiveMute(new UserId(user.Id), this.GuildId)).Match(mute => mute, _ => null),
            mute => mute is not null) ?? throw new ShouldAssertException($"{user.Username} has no active mute");

    /// <summary>Ends the mute as the expiry task would: the mute is recorded as removed and the role comes off.</summary>
    private async Task LiftMuteAsync(FakeUser user)
    {
        await this.ActiveMuteAsync(user);
        (await this.Settings.RemoveMute(new UserId(user.Id), this.GuildId, new ModeratorId(this.Guild.Bot.Id))).ShouldSucceed();
        this._bot.SetRolesByHand(user, [.. this.Guild.GetMember(user).RoleIds.Where(role => role != this._muteRoleId)]);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
    }

    private async Task<Sin> SeedMuteSinAsync(ModerationActor actor, DateTimeOffset at)
    {
        var sin = Sin.ForMute(actor, new UserId(this.Guild.User1.Id), this.GuildId, at).ShouldSucceed();
        await using var dbContext = this._database.CreateDbContext();
        dbContext.Sins.Add(sin);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return sin;
    }

    private async Task ShouldNotBeFilteredAsync()
    {
        await Task.Delay(HandlersSettle, TestContext.Current.CancellationToken);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        this._bot.Rest.Requests.ShouldNotContain(request => request.Method == HttpMethod.Put && request.Path.Contains("/roles/", StringComparison.Ordinal));
        this._bot.Logs.Errors.ShouldBeEmpty();
    }
}

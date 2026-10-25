// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/Ban</c>, <c>/Unban</c> and the ban gateway events, cases MOD-01 to MOD-42 in
///     <c>docs/testing/02-moderation-actions.md</c>. The fake acts like Discord: it refuses bans of anyone ranked at
///     or above the bot, writes the audit log, and sends <c>GUILD_BAN_ADD</c> and <c>GUILD_BAN_REMOVE</c>. Removing
///     the banned member's messages (MOD-03 to MOD-06) is Discord's job, so those tests check the requested window.
///     MOD-25 (two guilds) is in A-35.
/// </summary>
[Collection("Test collection")]
public sealed class BanTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string PermissionError = "You do not have the required permissions to use this command.";
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── /Ban ──────────────────────────────────────────────────────────────────

    /// <summary>MOD-01 and MOD-32: one ban, one sin, the reply, and exactly one mod-log embed.</summary>
    [Fact]
    public async Task Ban_BansRecordsTheSinRepliesAndLogsOnce()
    {
        var reply = await this.BanAsync(this.Guild.Admin, this.Guild.User1, SlashOption.String("reason", "spam"));

        var embed = reply.Embed!;
        embed["author"]!["name"]!.GetValue<string>().ShouldBe("Banned");
        Field(embed, "User").ShouldBe(this.Guild.User1.Mention);
        Field(embed, "Moderator").ShouldBe(this.Guild.Admin.MemberMention);
        Field(embed, "Reason").ShouldBe("spam");

        var put = this._bot.Rest.Requests.Single(this.IsBanOf(this.Guild.User1));
        put.AuditLogReason.ShouldBe("spam");
        this._bot.Rest.Banned.ShouldContain(this.Guild.User1.Id);

        var sin = await this.SingleSinAsync();
        sin.SinType.ShouldBe(SinType.Ban);
        sin.Actor.ShouldBe(new ModerationActor.Moderator(new ModeratorId(this.Guild.Admin.Id)));
        sin.ReasonHistory.ShouldHaveSingleItem().Reason.Value.ShouldBe("spam");
        Field(embed, "Sin Id").ShouldBe($"**{sin.Id.Value.ToString(CultureInfo.InvariantCulture)}**");

        var logged = await this._bot.WaitForModLogEmbedAsync(IsBannedEmbed);
        Field(logged, "Sin Id").ShouldBe($"**{sin.Id.Value.ToString(CultureInfo.InvariantCulture)}**");
        Field(logged, "Mod").ShouldBe($"<@{this.Guild.Admin.Id}>");
        Field(logged, "Reason").ShouldBe("spam");
        (await this.ModLogEmbedCountAsync(IsBannedEmbed)).ShouldBe(1);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-02.</summary>
    [Fact]
    public async Task Ban_WithoutAReason_ShowsNone_AndStoresNoReason()
    {
        Field((await this.BanAsync(this.Guild.Admin, this.Guild.User1)).Embed!, "Reason").ShouldBe("None");

        (await this.SingleSinAsync()).ReasonHistory.ShouldBeEmpty();
        Field(await this._bot.WaitForModLogEmbedAsync(IsBannedEmbed), "Reason").ShouldBe("None");
    }

    /// <summary>MOD-03 to MOD-06: the message-deletion window sent to Discord.</summary>
    [Theory]
    [InlineData(true, null, 7)]
    [InlineData(true, 1, 1)]
    [InlineData(false, 7, 0)]
    [InlineData(true, 0, 0)]
    public async Task Ban_AsksDiscordToDeleteTheRightDaysOfMessages(bool deleteMessages, int? deleteDays, int expectedDays)
    {
        SlashOption[] options = deleteDays is { } days
            ? [SlashOption.Boolean("delete_messages", deleteMessages), SlashOption.Integer("delete_days", days)]
            : [SlashOption.Boolean("delete_messages", deleteMessages)];

        await this.BanAsync(this.Guild.Admin, this.Guild.User1, options);

        this._bot.Rest.Requests.Single(this.IsBanOf(this.Guild.User1)).QueryValue("delete_message_seconds")
            .ShouldBe((expectedDays * 86_400).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>MOD-07 and MOD-08: Discord's client enforces these ranges.</summary>
    [Fact]
    public void Ban_IsRegisteredWithTheReasonAndDayLimits()
    {
        var options = this._bot.Rest.RegisteredCommands["ban"]["options"]!.AsArray();
        var reason = options.Single(option => option!["name"]!.GetValue<string>() == "reason")!;
        var days = options.Single(option => option!["name"]!.GetValue<string>() == "delete_days")!;

        reason["max_length"]!.GetValue<int>().ShouldBe(1000);
        days["min_value"]!.GetValue<int>().ShouldBe(0);
        days["max_value"]!.GetValue<int>().ShouldBe(7);
    }

    /// <summary>MOD-09.</summary>
    [Fact]
    public async Task Ban_ForAUserNotInTheServer_StillBansAndLogs()
    {
        var stranger = new FakeUser(1_100_000_000_000_000_096, "stranger");

        (await this.BanAsync(this.Guild.Admin, stranger)).Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Banned");

        this._bot.Rest.Banned.ShouldContain(stranger.Id);
        (await this.SingleSinAsync()).UserId.ShouldBe(new UserId(stranger.Id));
        await this._bot.WaitForModLogEmbedAsync(IsBannedEmbed);
    }

    /// <summary>MOD-10: banning someone already banned records a second sin, but Discord sends no second event.</summary>
    [Fact]
    public async Task Ban_OfSomeoneAlreadyBanned_RecordsASecondSin_ButLogsOnce()
    {
        await this.BanAsync(this.Guild.Admin, this.Guild.User1);
        await this._bot.WaitForModLogEmbedAsync(IsBannedEmbed);

        await this.BanAsync(this.Guild.Admin, this.Guild.User1);

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
        (await this.ModLogEmbedCountAsync(IsBannedEmbed)).ShouldBe(1);
    }

    /// <summary>MOD-12 and MOD-13: the gate is Manage Messages, so neither no permissions nor Ban Members alone is enough.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ban_WithoutManageMessages_IsDenied(bool hasBanMembers)
    {
        if (hasBanMembers)
            this.Guild.SetRoles(this.Guild.User1, this.Guild.BannerRoleId);

        (await this.BanAsync(this.Guild.User1, this.Guild.Admin)).Description.ShouldBe(PermissionError);

        await this.ShouldHaveNoSinsOrBansAsync();
    }

    /// <summary>MOD-14, MOD-15 and MOD-16: Manage Messages, the owner and Administrator are each enough.</summary>
    [Theory]
    [InlineData("manage messages")]
    [InlineData("owner")]
    [InlineData("admin")]
    public async Task Ban_WithManageMessagesOrABypass_IsAllowed(string who)
    {
        var moderator = who switch
        {
            "owner" => this.Guild.Owner,
            "admin" => this.Guild.Admin,
            _ => this.Guild.Moderator
        };

        (await this.BanAsync(moderator, this.Guild.User1)).Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Banned");

        (await this.SingleSinAsync()).Actor.ShouldBe(new ModerationActor.Moderator(new ModeratorId(moderator.Id)));
    }

    /// <summary>MOD-17: banning yourself is refused before anything happens.</summary>
    [Fact]
    public async Task Ban_OfYourself_IsRefused()
    {
        (await this.BanAsync(this.Guild.Moderator, this.Guild.Moderator)).Description.ShouldBe("You can't ban yourself.");

        await this.ShouldHaveNoSinsOrBansAsync();
    }

    /// <summary>MOD-18, MOD-19, MOD-20 and MOD-23: the bot can't act on anyone ranked at or above it.</summary>
    [Theory(Skip = "Fails: the hierarchy check only runs for a DiscordMember, which a slash option never is, so the bot " +
                   "tries the ban, Discord refuses it, and the sin is already saved. KI-19 and KI-03")]
    [InlineData("higher")]
    [InlineData("owner")]
    [InlineData("bot")]
    public async Task Ban_OfSomeoneTheBotCantOutrank_IsRefusedWithNoSin(string who)
    {
        var target = who switch
        {
            "owner" => this.Guild.Owner,
            "bot" => this.Guild.Bot,
            _ => this.Guild.Higher
        };

        (await this.BanAsync(this.Guild.Admin, target)).Description.ShouldBe("I do not have permissions to ban that user.");

        await this.ShouldHaveNoSinsOrBansAsync();
    }

    /// <summary>MOD-21: nothing checks that the moderator outranks the target.</summary>
    [Fact(Skip = "Fails: a moderator can ban someone ranked above them, as long as the bot outranks the target. KI-01")]
    public async Task Ban_OfSomeoneRankedAboveTheModerator_IsRefused()
    {
        await this.BanAsync(this.Guild.Moderator, this.Guild.Admin);

        await this.ShouldHaveNoSinsOrBansAsync();
    }

    /// <summary>MOD-22 and MOD-39.</summary>
    [Theory]
    [InlineData("ban")]
    [InlineData("unban")]
    public async Task WhenTheBotLacksBanMembers_IsRefused(string command)
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this._bot.ConfigureModerationAsync();

        var reply = await this._bot.RunSlashCommandAndWaitAsync(command, this.Guild.Admin, SlashOption.User("user", this.Guild.User1));

        reply.Description.ShouldBe("The bot did not have the needed permissions to execute this command.");
        await this.ShouldHaveNoSinsOrBansAsync();
    }

    /// <summary>MOD-24 and MOD-40.</summary>
    [Theory]
    [InlineData("ban")]
    [InlineData("unban")]
    public async Task WithModerationDisabled_IsRefused(string command)
    {
        await this._bot.SetModuleAsync(Module.Moderation, false);

        var reply = await this._bot.RunSlashCommandAndWaitAsync(command, this.Guild.Admin, SlashOption.User("user", this.Guild.User1));

        reply.Description.ShouldBe("This module is disabled in this server.");
        await this.ShouldHaveNoSinsOrBansAsync();
    }

    /// <summary>MOD-26: when Discord refuses the ban, no sin should be left behind.</summary>
    [Fact(Skip = "Fails: the sin is saved before calling Discord, so a refused ban leaves a phantom sin. KI-03")]
    public async Task Ban_ThatDiscordRefuses_LeavesNoSin()
    {
        this._bot.Rest.On(HttpMethod.Put, @"guilds/\d+/bans/\d+",
            _ => FakeDiscordRest.Error(HttpStatusCode.InternalServerError, 0, "500: Internal Server Error"));

        await this.BanAsync(this.Guild.Admin, this.Guild.User1);

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    // ── Bans from Discord's client ────────────────────────────────────────────

    /// <summary>MOD-27 and MOD-28: the audit log supplies the moderator and reason.</summary>
    [Theory]
    [InlineData("manual")]
    [InlineData(null)]
    public async Task ClientBan_IsRecordedFromTheAuditLog(string? reason)
    {
        this._bot.BanFromClient(this.Guild.Moderator, this.Guild.User1, reason);

        var logged = await this._bot.WaitForModLogEmbedAsync(IsBannedEmbed);
        Field(logged, "Mod").ShouldBe($"<@{this.Guild.Moderator.Id}>");
        Field(logged, "Reason").ShouldBe(reason ?? "None");

        var sin = await this.SingleSinAsync();
        sin.Actor.ShouldBe(new ModerationActor.Moderator(new ModeratorId(this.Guild.Moderator.Id)));
        sin.ReasonHistory.Select(history => history.Reason.Value).ShouldBe(reason is null ? [] : [reason]);
    }

    /// <summary>
    ///     A client ban the audit log can't attribute (no matching entry) is recorded with a System actor, and logged
    ///     without a Mod field.
    /// </summary>
    [Fact(Skip = "Fails: a sin with a System actor can't be saved; EF throws \"Nullable object must have a value\", so " +
                 "the ban is never recorded or logged. KI-31")]
    public async Task ClientBan_TheAuditLogCannotAttribute_IsRecordedAsSystem()
    {
        this._bot.Rest.RememberUser(this.Guild.User1);
        this._bot.Rest.MarkBanned(this.Guild.User1.Id);
        this._bot.Gateway.Dispatch("GUILD_BAN_ADD", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["user"] = this.Guild.User1.ToJson()
        });

        var logged = await this._bot.WaitForModLogEmbedAsync(IsBannedEmbed);
        logged["fields"]!.AsArray().Select(field => field!["name"]!.GetValue<string>()).ShouldNotContain("Mod");
        (await this.SingleSinAsync()).Actor.ShouldBe(new ModerationActor.System());
    }

    /// <summary>MOD-29: without View Audit Log the ban can't be attributed, so a guild issue is posted instead.</summary>
    [Fact]
    public async Task ClientBan_WithoutViewAuditLog_ReportsAnIssue_AndRecordsNothing()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this._bot.ConfigureModerationAsync();

        this._bot.BanFromClient(this.Guild.Moderator, this.Guild.User1, "manual");

        await this._bot.WaitForModLogEmbedAsync(embed =>
            embed["description"]?.GetValue<string>().Contains("cannot read the audit log", StringComparison.Ordinal) == true);
        (await this.ModLogEmbedCountAsync(IsBannedEmbed)).ShouldBe(0);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>MOD-30: a ban within 30 seconds of the last one reuses that sin.</summary>
    [Fact]
    public async Task ClientBan_WithinThirtySecondsOfTheLastBan_ReusesItsSin()
    {
        await this.BanAsync(this.Guild.Admin, this.Guild.User1, SlashOption.String("reason", "first"));
        await this._bot.WaitForModLogEmbedAsync(IsBannedEmbed);
        this._bot.UnbanFromClient(this.Guild.Admin, this.Guild.User1);

        this._bot.BanFromClient(this.Guild.Moderator, this.Guild.User1, "second");

        var embeds = await GrimoireTestBot.EventuallyAsync(
            () => Task.FromResult(this.ModLogEmbeds(IsBannedEmbed)), found => found.Count >= 2);
        embeds.Count.ShouldBe(2);
        Field(embeds[1], "Mod").ShouldBe($"<@{this.Guild.Admin.Id}>");
        Field(embeds[1], "Reason").ShouldBe("first");
        (await this.SingleSinAsync()).Actor.ShouldBe(new ModerationActor.Moderator(new ModeratorId(this.Guild.Admin.Id)));
    }

    /// <summary>MOD-31 and MOD-41.</summary>
    [Fact]
    public async Task ClientBanAndUnban_WithModerationDisabled_AreIgnored()
    {
        await this._bot.SetModuleAsync(Module.Moderation, false);

        this._bot.BanFromClient(this.Guild.Moderator, this.Guild.User1, "manual");
        this._bot.UnbanFromClient(this.Guild.Moderator, this.Guild.User1);

        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    // ── /Unban ────────────────────────────────────────────────────────────────

    /// <summary>MOD-33: the reply names the moderator, and the mod log names the original banner (KI-09).</summary>
    [Fact]
    public async Task Unban_LiftsTheBan_AndLogsTheBansSin()
    {
        await this.BanAsync(this.Guild.Admin, this.Guild.User1);
        var sin = await this.SingleSinAsync();

        var reply = await this._bot.RunSlashCommandAndWaitAsync("unban", this.Guild.Moderator, SlashOption.User("user", this.Guild.User1));

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Unbanned");
        Field(reply.Embed!, "User").ShouldBe(this.Guild.User1.Mention);
        Field(reply.Embed!, "Moderator").ShouldBe(this.Guild.Moderator.MemberMention);
        this._bot.Rest.Banned.ShouldNotContain(this.Guild.User1.Id);

        var logged = await this._bot.WaitForModLogEmbedAsync(IsUnbannedEmbed);
        Field(logged, "Sin Id").ShouldBe($"**{sin.Id.Value.ToString(CultureInfo.InvariantCulture)}**");
        // KI-09: this is the original banner, not the moderator who unbanned.
        Field(logged, "Mod").ShouldBe($"<@{this.Guild.Admin.Id}>");
        (await this.SingleSinAsync()).Id.ShouldBe(sin.Id);
    }

    /// <summary>MOD-34 and MOD-42: with several ban sins on record, a client unban logs the newest.</summary>
    [Fact]
    public async Task ClientUnban_LogsTheNewestBanSin()
    {
        await this.SeedBanSinsAsync(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1));
        this._bot.Rest.RememberUser(this.Guild.User1);
        this._bot.Rest.MarkBanned(this.Guild.User1.Id);
        await using var dbContext = this._database.CreateDbContext();
        var newest = await dbContext.Sins.OrderByDescending(sin => sin.SinOn).FirstAsync(TestContext.Current.CancellationToken);

        this._bot.UnbanFromClient(this.Guild.Moderator, this.Guild.User1);

        Field(await this._bot.WaitForModLogEmbedAsync(IsUnbannedEmbed), "Sin Id")
            .ShouldBe($"**{newest.Id.Value.ToString(CultureInfo.InvariantCulture)}**");
    }

    /// <summary>MOD-35: with no ban sin on record, there is nothing to log.</summary>
    [Fact]
    public async Task ClientUnban_WithNoBanSinOnRecord_LogsNothing()
    {
        this._bot.Rest.RememberUser(this.Guild.User1);
        this._bot.Rest.MarkBanned(this.Guild.User1.Id);

        this._bot.UnbanFromClient(this.Guild.Moderator, this.Guild.User1);

        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>MOD-36: KI-10 notes this wording is misleading; this pins the current text.</summary>
    [Fact]
    public async Task Unban_OfSomeoneNotBanned_SaysTheyCouldNotBeFound()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("unban", this.Guild.Admin, SlashOption.User("user", this.Guild.User1));

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Error");
        reply.Description.ShouldBe("user1 was not unbanned because user could not be found.");
    }

    /// <summary>MOD-37 and MOD-38.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unban_NeedsManageMessages(bool hasManageMessages)
    {
        this._bot.BanFromClient(this.Guild.Admin, this.Guild.Higher);
        if (hasManageMessages)
            this.Guild.SetRoles(this.Guild.User1, this.Guild.ModeratorRoleId);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("unban", this.Guild.User1, SlashOption.User("user", this.Guild.Higher));

        if (hasManageMessages)
            reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Unbanned");
        else
            reply.Description.ShouldBe(PermissionError);
    }

    private Task<FakeReply> BanAsync(FakeUser moderator, FakeUser target, params SlashOption[] extra)
        => this._bot.RunSlashCommandAndWaitAsync("ban", moderator, [SlashOption.User("user", target), .. extra]);

    private Func<RecordedRequest, bool> IsBanOf(FakeUser target)
        => request => request.Method == HttpMethod.Put && request.Path == $"guilds/{this.Guild.Id}/bans/{target.Id}";

    private static bool IsBannedEmbed(JsonNode embed) => embed["author"]?["name"]?.GetValue<string>() == "Banned";

    private static bool IsUnbannedEmbed(JsonNode embed) => embed["author"]?["name"]?.GetValue<string>() == "Unbanned";

    private List<JsonNode> ModLogEmbeds(Func<JsonNode, bool> match)
        => [.. this._bot.Rest.Requests.Where(this._bot.IsModLogPost)
            .Select(request => request.Json?["embeds"]?[0]).OfType<JsonNode>().Where(match)];

    /// <summary>Counts matching mod-log embeds once the log has had time to post anything else.</summary>
    private async Task<int> ModLogEmbedCountAsync(Func<JsonNode, bool> match)
    {
        await Task.Delay(NothingHappensWindow, TestContext.Current.CancellationToken);
        return this.ModLogEmbeds(match).Count;
    }

    private async Task<Sin> SingleSinAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.Sins.Include(sin => sin.ReasonHistory).SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task ShouldHaveNoSinsOrBansAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        this._bot.Rest.Banned.ShouldBeEmpty();
    }

    private async Task SeedBanSinsAsync(params DateTimeOffset[] times)
    {
        await using var seed = this._database.CreateDbContext();
        var actor = new ModerationActor.Moderator(new ModeratorId(this.Guild.Admin.Id));
        foreach (var at in times)
            seed.Sins.Add(Sin.ForBan(actor, new UserId(this.Guild.User1.Id), new GuildId(this.Guild.Id), at).ShouldSucceed());
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

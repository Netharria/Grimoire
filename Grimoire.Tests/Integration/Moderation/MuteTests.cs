// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Features.Shared.SharedDtos;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/Mute</c> and <c>/Unmute</c>, cases MOD-90 to MOD-123 in <c>docs/testing/02-moderation-actions.md</c>. Both
///     take a <c>DiscordMember</c>, so DMs are sent. Expiry, restarts and rejoining (MOD-99, MOD-123) are in A-11, and
///     MOD-111 (two guilds) in A-35. MOD-108 depends on whether Discord checks the target's rank for role grants, so it
///     stays manual.
/// </summary>
[Collection("Test collection")]
public sealed class MuteTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string PermissionError = "You do not have the required permissions to use this command.";

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;
    private ulong _muteRoleId;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        this._muteRoleId = await this.ConfigureMuteRoleAsync("Muted");
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── /Mute ─────────────────────────────────────────────────────────────────

    /// <summary>MOD-90: the reply, the role, the DM after the reply, the records and the mod log.</summary>
    [Fact]
    public async Task Mute_GrantsTheRole_RecordsIt_RepliesDmsAndLogs()
    {
        var before = DateTimeOffset.UtcNow;

        var reply = await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 2, "cooldown");

        var embed = reply.Embed!;
        embed["author"]!["name"]!.GetValue<string>().ShouldBe("Mute");
        Field(embed, "User").ShouldBe(this.Guild.User1.MemberMention);
        Field(embed, "Moderator").ShouldBe(this.Guild.Moderator.MemberMention);
        Field(embed, "Length").ShouldBe("2 Minutes");
        Field(embed, "Reason").ShouldBe("cooldown");

        this._bot.Rest.Requests.Single(this.IsRoleChange(HttpMethod.Put, this.Guild.User1)).AuditLogReason.ShouldBe("cooldown");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(this._muteRoleId);

        var dm = await this.WaitForDmAsync(this.Guild.User1);
        var sin = await this.SingleSinAsync();
        dm["author"]!["name"]!.GetValue<string>().ShouldBe($"Mute Id {sin.Id.Value.ToString(CultureInfo.InvariantCulture)}");
        dm["description"]!.GetValue<string>().ShouldBe($"You have been muted for 2 Minutes by {this.Guild.Moderator.MemberMention} for cooldown");
        this.IndexOf(request => request.Path == $"channels/{this.DmChannelOf(this.Guild.User1)}/messages")
            .ShouldBeGreaterThan(this.IndexOf(request => request.Method == HttpMethod.Patch), "the embed is sent before the DM");

        sin.SinType.ShouldBe(SinType.Mute);
        sin.ReasonHistory.ShouldHaveSingleItem().Reason.Value.ShouldBe("cooldown");
        var mute = (await this.Settings.GetActiveMute(new UserId(this.Guild.User1.Id), this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed();
        mute.SinId.ShouldBe(sin.Id);
        mute.MuteRoleId.ShouldBe(new RoleId(this._muteRoleId));
        mute.EndTime.ShouldBeInRange(before.AddMinutes(2), DateTimeOffset.UtcNow.AddMinutes(2));

        await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Mute");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-91.</summary>
    [Theory]
    [InlineData(DurationType.Hours, 1, "1 Hours", 60)]
    [InlineData(DurationType.Days, 1, "1 Days", 1440)]
    public async Task Mute_SetsTheEndTimeForEachDurationType(DurationType type, int amount, string length, int minutes)
    {
        var before = DateTimeOffset.UtcNow;

        Field((await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, type, amount)).Embed!, "Length").ShouldBe(length);

        (await this.Settings.GetActiveMute(new UserId(this.Guild.User1.Id), this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .EndTime.ShouldBeInRange(before.AddMinutes(minutes), DateTimeOffset.UtcNow.AddMinutes(minutes));
    }

    /// <summary>MOD-93: a zero-length mute should be recorded, so the expiry task can lift it.</summary>
    [Fact(Skip = "Fails: MuteAdded rejects an end time that isn't after its set time, /Mute ignores the failed AddMute, " +
                 "and the role is granted with no record, so the mute never expires. KI-07")]
    public async Task Mute_ForZeroMinutes_IsRecordedSoItExpires()
    {
        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 0);

        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.OfType<MuteAdded>().AnyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    /// <summary>MOD-92: without a reason there is no Reason field and nothing stored.</summary>
    [Fact]
    public async Task Mute_WithoutAReason_HasNoReasonField()
    {
        var reply = await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 2);

        FieldNames(reply.Embed!).ShouldNotContain("Reason");
        (await this.SingleSinAsync()).ReasonHistory.ShouldBeEmpty();
    }

    /// <summary>MOD-92, observation 5.</summary>
    [Fact(Skip = "Fails: with no reason the DM still ends \"... for \" with nothing after it. KI-05")]
    public async Task Mute_WithoutAReason_DmsWithoutADanglingFor()
    {
        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 2);

        (await this.WaitForDmAsync(this.Guild.User1))["description"]!.GetValue<string>()
            .ShouldBe($"You have been muted for 2 Minutes by {this.Guild.Moderator.MemberMention}");
    }

    /// <summary>MOD-94 and MOD-97: Discord's client enforces these limits.</summary>
    [Fact]
    public void Mute_IsRegisteredWithTheDurationAndReasonLimits()
    {
        var options = this._bot.Rest.RegisteredCommands["mute"]["options"]!.AsArray();
        options.Single(o => o!["name"]!.GetValue<string>() == "duration_amount")!["min_value"]!.GetValue<int>().ShouldBe(0);
        options.Single(o => o!["name"]!.GetValue<string>() == "reason")!["max_length"]!.GetValue<int>().ShouldBe(1000);
    }

    /// <summary>MOD-95.</summary>
    [Fact]
    public async Task Mute_WhenTheMemberHasDmsClosed_TellsTheModLog()
    {
        this.CloseDms(this.Guild.User1);

        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 2);

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"Was not able to send a direct message with the mute details to {this.Guild.User1.MemberMention}.");
        await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Mute");
    }

    /// <summary>MOD-96: muting again records a second sin and mute, and the newer mute is the active one.</summary>
    [Fact]
    public async Task Mute_Again_RecordsASecondMute_ThatTakesOver()
    {
        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 2);
        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Days, 1);

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.OfType<MuteAdded>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
        (await this.Settings.GetActiveMute(new UserId(this.Guild.User1.Id), this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .EndTime.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddHours(23));
    }

    /// <summary>MOD-100 to MOD-102: the gate is Manage Messages, with owner and Administrator bypasses.</summary>
    [Theory]
    [InlineData("none", false)]
    [InlineData("manage messages", true)]
    [InlineData("owner", true)]
    [InlineData("admin", true)]
    public async Task Mute_NeedsManageMessages(string who, bool allowed)
    {
        var moderator = who switch { "owner" => this.Guild.Owner, "admin" => this.Guild.Admin, _ => this.Guild.User1 };
        if (who == "manage messages")
            this.Guild.SetRoles(this.Guild.User1, this.Guild.ModeratorRoleId);

        var reply = await this.MuteAsync(moderator, this.Guild.Moderator == moderator ? this.Guild.User1 : this.Guild.Moderator,
            DurationType.Minutes, 2);

        (reply.Description == PermissionError).ShouldBe(!allowed);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBe(allowed);
    }

    /// <summary>MOD-103.</summary>
    [Fact]
    public async Task Mute_WithNoMuteRoleConfigured_IsRefused_AndRecordsNothing()
    {
        (await this.Settings.DisableMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();

        (await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 2)).Description
            .ShouldBe("The mute role is not configured. Please configure it before using this command.");

        await this.ShouldHaveNoRecordsAsync();
    }

    /// <summary>MOD-104, MOD-105, MOD-121 and MOD-122.</summary>
    [Theory]
    [InlineData("mute", "module")]
    [InlineData("unmute", "module")]
    [InlineData("mute", "bot")]
    [InlineData("unmute", "bot")]
    public async Task MuteAndUnmute_AreRefusedWithoutTheModuleOrManageRoles(string command, string missing)
    {
        if (missing == "bot")
        {
            await this._bot.DisposeAsync();
            this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
                FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
            await this._bot.ConfigureModerationAsync();
        }
        else
            await this._bot.SetModuleAsync(Module.Moderation, false);

        SlashOption[] options = command == "mute"
            ? [SlashOption.User("user", this.Guild.User1), SlashOption.Enum("duration_type", DurationType.Minutes), SlashOption.Integer("duration_amount", 2)]
            : [SlashOption.User("user", this.Guild.User1)];
        var reply = await this._bot.RunSlashCommandAndWaitAsync(command, this.Guild.Moderator, options);

        reply.Description.ShouldBe(missing == "bot"
            ? "The bot did not have the needed permissions to execute this command."
            : "This module is disabled in this server.");
        await this.ShouldHaveNoRecordsAsync();
    }

    /// <summary>MOD-106: the configured role was deleted in Discord.</summary>
    [Fact]
    public async Task Mute_WithTheConfiguredRoleDeleted_SaysSo()
    {
        this._bot.Rest.DeleteRole(this._muteRoleId);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        (await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 2)).Description
            .ShouldBe("The configured mute role does not exist. Please configure it again before using this command.");
    }

    /// <summary>MOD-106 and MOD-107: a refused or impossible mute should leave no sin or mute behind.</summary>
    [Theory(Skip = "Fails: the Sin and MuteAdded rows are saved before the role is checked or granted. KI-04")]
    [InlineData("deleted")]
    [InlineData("above the bot")]
    public async Task Mute_ThatCannotGrantTheRole_LeavesNoRecords(string problem)
    {
        if (problem == "deleted")
            this._bot.Rest.DeleteRole(this._muteRoleId);
        else
            this._muteRoleId = await this.ConfigureMuteRoleAsync("Muted above", FakeGuild.RoleAboveTheBot);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 2);

        await this.ShouldHaveNoRecordsAsync();
    }

    /// <summary>MOD-107: a mute role above the bot's is refused by Discord, and the error handler explains it.</summary>
    [Fact]
    public async Task Mute_WithTheRoleAboveTheBot_ShowsTheMissingPermissionsHelp()
    {
        this._muteRoleId = await this.ConfigureMuteRoleAsync("Muted above", FakeGuild.RoleAboveTheBot);
        var issue = GuildIssueCatalog.BotMissingPermissions(this.GuildId, "Mute");

        (await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 2)).Description
            .ShouldBe($"{issue.Summary}\n**Suggested fix:** {issue.SuggestedFix}");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this._muteRoleId);
    }

    /// <summary>MOD-109: nothing checks that the moderator outranks the target.</summary>
    [Fact(Skip = "Fails: a moderator can mute someone ranked above them. KI-01")]
    public async Task Mute_OfSomeoneRankedAboveTheModerator_IsRefused()
    {
        await this.MuteAsync(this.Guild.Moderator, this.Guild.Admin, DurationType.Minutes, 2);

        await this.ShouldHaveNoRecordsAsync();
    }

    /// <summary>MOD-110: there's no self-mute check. This pins the current behaviour.</summary>
    [Fact]
    public async Task Mute_OfYourself_IsAllowed()
    {
        (await this.MuteAsync(this.Guild.Moderator, this.Guild.Moderator, DurationType.Minutes, 2)).Embed!["author"]!["name"]!
            .GetValue<string>().ShouldBe("Mute");
        this.Guild.GetMember(this.Guild.Moderator).RoleIds.ShouldContain(this._muteRoleId);
    }

    // ── /Unmute ───────────────────────────────────────────────────────────────

    /// <summary>MOD-112: the role comes off, a MuteRemoved row is added, and the member hears about it.</summary>
    [Fact]
    public async Task Unmute_RemovesTheRole_RecordsIt_RepliesDmsAndLogs()
    {
        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 10);

        var reply = await this.UnmuteAsync(this.Guild.Moderator, this.Guild.User1);

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Unmute");
        Field(reply.Embed!, "User").ShouldBe(this.Guild.User1.MemberMention);
        Field(reply.Embed!, "Moderator").ShouldBe(this.Guild.Moderator.MemberMention);
        this._bot.Rest.Requests.Single(this.IsRoleChange(HttpMethod.Delete, this.Guild.User1)).AuditLogReason
            .ShouldBe($"Unmuted by {this.Guild.Moderator.MemberMention}");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this._muteRoleId);

        (await GrimoireTestBot.EventuallyAsync(() => this.DmsTo(this.Guild.User1), dms => dms.Count >= 2))[^1]["description"]!
            .GetValue<string>().ShouldBe($"You have been unmuted by {this.Guild.Moderator.MemberMention}");
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.OfType<MuteRemoved>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await settingsContext.Mutes.OfType<MuteAdded>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Unmute");
    }

    /// <summary>MOD-113.</summary>
    [Fact]
    public async Task Unmute_WhenTheMemberHasDmsClosed_TellsTheModLog()
    {
        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 10);
        this.CloseDms(this.Guild.User1);

        await this.UnmuteAsync(this.Guild.Moderator, this.Guild.User1);

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"Was not able to send a direct message with the unmute details to {this.Guild.User1.MemberMention}");
    }

    /// <summary>MOD-114.</summary>
    [Fact(Skip = "Fails: unmuting someone who isn't muted still revokes, replies \"Unmute\", DMs and logs. KI-11")]
    public async Task Unmute_OfSomeoneNotMuted_SaysSo()
        => (await this.UnmuteAsync(this.Guild.Moderator, this.Guild.User1)).Embed?["author"]?["name"]?.GetValue<string>()
            .ShouldNotBe("Unmute");

    /// <summary>MOD-115: a hand-added mute role with no record falls back to the configured role.</summary>
    [Fact]
    public async Task Unmute_WithNoRecord_RemovesTheConfiguredRole()
    {
        this.Guild.GrantRole(this.Guild.User1, this._muteRoleId);

        await this.UnmuteAsync(this.Guild.Moderator, this.Guild.User1);

        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this._muteRoleId);
    }

    /// <summary>MOD-116: the role the member was muted with is removed, even after the setting changed.</summary>
    [Fact]
    public async Task Unmute_RemovesTheRecordedRole_NotTheNewlyConfiguredOne()
    {
        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 10);
        var newRole = await this.ConfigureMuteRoleAsync("Muted B");

        await this.UnmuteAsync(this.Guild.Moderator, this.Guild.User1);

        this._bot.Rest.Requests.Single(this.IsRoleChange(HttpMethod.Delete, this.Guild.User1)).Path
            .ShouldEndWith($"/roles/{this._muteRoleId}");
        newRole.ShouldNotBe(this._muteRoleId);
    }

    /// <summary>MOD-117: when the recorded role is gone, the configured role is used instead.</summary>
    [Fact]
    public async Task Unmute_WithTheRecordedRoleDeleted_FallsBackToTheConfiguredRole()
    {
        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 10);
        var recorded = this._muteRoleId;
        var configured = await this.ConfigureMuteRoleAsync("Muted B");
        this._bot.Rest.DeleteRole(recorded);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        (await this.UnmuteAsync(this.Guild.Moderator, this.Guild.User1)).Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Unmute");

        this._bot.Rest.Requests.Single(this.IsRoleChange(HttpMethod.Delete, this.Guild.User1)).Path
            .ShouldEndWith($"/roles/{configured}");
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.OfType<MuteRemoved>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    /// <summary>MOD-118: with no role left to remove, the record is cleared and the moderator is told.</summary>
    [Fact]
    public async Task Unmute_WithNoRoleLeft_ClearsTheMuteAndExplains()
    {
        await this.MuteAsync(this.Guild.Moderator, this.Guild.User1, DurationType.Minutes, 10);
        this._bot.Rest.DeleteRole(this._muteRoleId);
        (await this.Settings.DisableMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        var modLogPostsBefore = this._bot.Rest.Requests.Count(this._bot.IsModLogPost);

        var reply = await this.UnmuteAsync(this.Guild.Moderator, this.Guild.User1);

        reply.Description.ShouldNotBeNull().ShouldStartWith("The role this member was muted with no longer exists");
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.OfType<MuteRemoved>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        this._bot.Rest.Requests.Count(this._bot.IsModLogPost).ShouldBe(modLogPostsBefore);
    }

    /// <summary>MOD-119: when Discord refuses the revoke, the mute stays recorded so it can be retried.</summary>
    [Fact]
    public async Task Unmute_ThatDiscordRefuses_KeepsTheMuteRecorded()
    {
        this._muteRoleId = await this.ConfigureMuteRoleAsync("Muted above", FakeGuild.RoleAboveTheBot);
        this.Guild.GrantRole(this.Guild.User1, this._muteRoleId);
        (await this.Settings.AddMute(new UserId(this.Guild.User1.Id), this.GuildId, new ModeratorId(this.Guild.Moderator.Id), new SinId(1), DateTimeOffset.UtcNow.AddHours(1), new RoleId(this._muteRoleId), TestContext.Current.CancellationToken)).ShouldSucceed();
        var issue = GuildIssueCatalog.BotMissingPermissions(this.GuildId, "Unmute");

        (await this.UnmuteAsync(this.Guild.Moderator, this.Guild.User1)).Description
            .ShouldBe($"{issue.Summary}\n**Suggested fix:** {issue.SuggestedFix}");

        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.OfType<MuteRemoved>().AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>MOD-120.</summary>
    [Fact]
    public async Task Unmute_WithoutManageMessages_IsDenied()
        => (await this.UnmuteAsync(this.Guild.User1, this.Guild.Moderator)).Description.ShouldBe(PermissionError);

    private async Task<ulong> ConfigureMuteRoleAsync(string name, int position = 1)
    {
        var roleId = this._bot.Rest.CreateRole(name, 0, position);
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(roleId))).ShouldSucceed();
        return roleId;
    }

    private Task<FakeReply> MuteAsync(FakeUser moderator, FakeUser target, DurationType type, int amount, string? reason = null)
        => this._bot.RunSlashCommandAndWaitAsync("mute", moderator,
        [
            SlashOption.User("user", target), SlashOption.Enum("duration_type", type), SlashOption.Integer("duration_amount", amount),
            .. reason is null ? Array.Empty<SlashOption>() : [SlashOption.String("reason", reason)]
        ]);

    private Task<FakeReply> UnmuteAsync(FakeUser moderator, FakeUser target)
        => this._bot.RunSlashCommandAndWaitAsync("unmute", moderator, SlashOption.User("user", target));

    private Func<RecordedRequest, bool> IsRoleChange(HttpMethod method, FakeUser target)
        => request => request.Method == method && request.Path.StartsWith($"guilds/{this.Guild.Id}/members/{target.Id}/roles/", StringComparison.Ordinal);

    /// <summary>The fake gives each member a DM channel id of their own id plus 1000.</summary>
    private ulong DmChannelOf(FakeUser user) => user.Id + 1_000;

    private void CloseDms(FakeUser user)
        => this._bot.Rest.On(HttpMethod.Post, $"channels/{this.DmChannelOf(user)}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50007, "Cannot send messages to this user"));

    private Task<List<JsonNode>> DmsTo(FakeUser user)
        => Task.FromResult<List<JsonNode>>([.. this._bot.Rest.Requests
            .Where(request => request.Method == HttpMethod.Post && request.Path == $"channels/{this.DmChannelOf(user)}/messages")
            .Select(request => request.Json!["embeds"]![0]!)]);

    private async Task<JsonNode> WaitForDmAsync(FakeUser user)
        => (await this._bot.Rest.WaitForRequestAsync(request =>
            request.Method == HttpMethod.Post && request.Path == $"channels/{this.DmChannelOf(user)}/messages")).Json!["embeds"]![0]!;

    private int IndexOf(Func<RecordedRequest, bool> match)
        => this._bot.Rest.Requests.Select((request, index) => (request, index)).First(pair => match(pair.request)).index;

    private async Task<Sin> SingleSinAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.Sins.Include(sin => sin.ReasonHistory).SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task ShouldHaveNoRecordsAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    private static string[] FieldNames(JsonNode embed)
        => [.. embed["fields"]!.AsArray().Select(field => field!["name"]!.GetValue<string>())];

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

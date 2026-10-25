// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using Grimoire.Features.Moderation.Mute;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     Mute expiry (<c>MuteBackgroundTasks</c>) and rejoining while muted (<c>UserJoinedWhileMuted</c>), cases
///     MOD-124 to MOD-140 in <c>docs/testing/02-moderation-actions.md</c>. Tests run one expiry tick on demand. One
///     test starts the real timer. MOD-130 (the bot stopped while a member rejoined) is covered by the expiry and rejoin
///     tests separately, MOD-135 (a retry after 30 seconds of backoff) stays manual, and MOD-141 to MOD-143 depend on
///     <c>/Mutes View</c> listing members at all (KI-26).
/// </summary>
[Collection("Test collection")]
public sealed class MuteExpiryTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

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
        this._muteRoleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(this._muteRoleId))).ShouldSucceed();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Expiry ────────────────────────────────────────────────────────────────

    /// <summary>MOD-124: the role comes off, the member and mod log are told, and the bot records the removal.</summary>
    [Fact]
    public async Task ExpiredMute_RemovesTheRole_TellsEveryone_AndRecordsIt()
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: true);

        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();

        this._bot.Rest.Requests.Single(this.IsRoleRevoke(this.Guild.User1)).AuditLogReason.ShouldBe("Mute expired");
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldNotContain(this._muteRoleId);
        var dm = await this._bot.Rest.WaitForRequestAsync(this.IsDmTo(this.Guild.User1));
        dm.Json!["embeds"]![0]!["description"]!.GetValue<string>().ShouldBe($"Mute on {this.Guild.User1.MemberMention} has expired.");
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"Mute on {this.Guild.User1.MemberMention} has expired.");
        (await this.RemovalByAsync()).ShouldBe(this.Guild.Bot.Id);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-125: closed DMs don't stop the expiry or the log.</summary>
    [Fact]
    public async Task ExpiredMute_WithDmsClosed_StillExpiresAndLogs()
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: true);
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Guild.User1.Id + 1_000}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50007, "Cannot send messages to this user"));

        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.EndsWith("has expired.", StringComparison.Ordinal) == true);
        (await this.RemovalByAsync()).ShouldNotBeNull();
    }

    /// <summary>MOD-126: when a moderator already took the role off, nothing is revoked but everyone is still told.</summary>
    [Fact]
    public async Task ExpiredMute_WhoseRoleWasRemovedByHand_SkipsTheRevoke()
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: true, giveRole: false);

        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();

        this._bot.Rest.Requests.Count(this.IsRoleRevoke(this.Guild.User1)).ShouldBe(0);
        await this._bot.Rest.WaitForRequestAsync(this.IsDmTo(this.Guild.User1));
        (await this.RemovalByAsync()).ShouldNotBeNull();
    }

    /// <summary>MOD-127: a member who left gets nothing, but the mute is closed.</summary>
    [Fact]
    public async Task ExpiredMute_OfAMemberWhoLeft_IsClosedQuietly()
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: true);
        this._bot.MemberLeaves(this.Guild.User1);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();

        (await this.RemovalByAsync()).ShouldNotBeNull();
        this._bot.Rest.Requests.Count(this.IsDmTo(this.Guild.User1)).ShouldBe(0);
        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>MOD-134: when Discord refuses the revoke, an issue is raised, the mute stays, and it isn't retried at once.</summary>
    [Fact]
    public async Task ExpiredMute_ThatCannotBeRevoked_ReportsAnIssue_AndBacksOff()
    {
        var aboveTheBot = this._bot.Rest.CreateRole("Muted above", 0, FakeGuild.RoleAboveTheBot);
        await this.MuteByHandAsync(this.Guild.User1, expired: true, roleId: aboveTheBot);

        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();
        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.Contains(this.Guild.User1.Mention, StringComparison.Ordinal) == true);
        (await this.RemovalByAsync()).ShouldBeNull();
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(aboveTheBot);
        this._bot.Rest.Requests.Count(request => request.Method == HttpMethod.Delete
                                                 && request.Path.StartsWith($"guilds/{this.Guild.Id}/members/{this.Guild.User1.Id}/roles/", StringComparison.Ordinal))
            .ShouldBe(1, "the second tick is inside the backoff window");
    }

    /// <summary>MOD-136 and MOD-137: with no role left, the mute is closed; an issue is raised only if one is configured.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiredMute_WithItsRoleDeleted_IsClosed(bool settingStillPointsAtIt)
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: true, giveRole: false);
        this._bot.Rest.DeleteRole(this._muteRoleId);
        if (!settingStillPointsAtIt)
            (await this.Settings.DisableMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();

        (await this.RemovalByAsync()).ShouldNotBeNull();
        (await this._bot.Rest.ReceivesRequestWithinAsync(request =>
                this._bot.IsModLogPost(request) && request.Json?["embeds"]?[0]?["description"]?.ToString().Contains("mute role", StringComparison.OrdinalIgnoreCase) == true,
            NothingHappensWindow))
            .ShouldBe(settingStillPointsAtIt);
    }

    /// <summary>MOD-138: the role the member was muted with comes off, not the newly configured one.</summary>
    [Fact]
    public async Task ExpiredMute_RemovesTheRecordedRole()
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: true);
        var newRole = this._bot.Rest.CreateRole("Muted B");
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(newRole), TestContext.Current.CancellationToken)).ShouldSucceed();

        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();

        this._bot.Rest.Requests.Single(this.IsRoleRevoke(this.Guild.User1)).Path.ShouldEndWith($"/roles/{this._muteRoleId}");
    }

    /// <summary>MOD-139: a mute in a guild the bot isn't in is kept and left alone.</summary>
    [Fact]
    public async Task ExpiredMute_InAGuildTheBotLeft_IsKept()
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: true, guildId: new GuildId(1_100_000_000_000_000_999), giveRole: false);
        var requestsBefore = this._bot.Rest.Requests.Count;

        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();

        (await this.RemovalByAsync()).ShouldBeNull();
        this._bot.Rest.Requests.Count.ShouldBe(requestsBefore);
    }

    /// <summary>MOD-140: a failure on one mute doesn't block the others in the same tick.</summary>
    [Fact]
    public async Task ExpiredMutes_AreHandledIndependently()
    {
        var aboveTheBot = this._bot.Rest.CreateRole("Muted above", 0, FakeGuild.RoleAboveTheBot);
        await this.MuteByHandAsync(this.Guild.User1, expired: true, roleId: aboveTheBot);
        await this.MuteByHandAsync(this.Guild.Admin, expired: true);

        await this._bot.RunBackgroundTickAsync<MuteBackgroundTasks>();

        this.Guild.GetMember(this.Guild.Admin).RoleIds.ShouldNotContain(this._muteRoleId);
        this.Guild.GetMember(this.Guild.User1).RoleIds.ShouldContain(aboveTheBot);
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.OfType<MuteRemoved>().Select(mute => mute.UserId).ToListAsync(TestContext.Current.CancellationToken))
            .ShouldBe([new UserId(this.Guild.Admin.Id)]);
    }

    /// <summary>The real timer picks up an expired mute without being ticked by hand.</summary>
    [Fact]
    public async Task TheRealTimer_ExpiresMutes()
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: true);

        await this._bot.StartBackgroundServiceAsync<MuteBackgroundTasks>();

        (await GrimoireTestBot.EventuallyAsync(this.RemovalByAsync, removedBy => removedBy is not null, TimeSpan.FromSeconds(15)))
            .ShouldBe(this.Guild.Bot.Id);
    }

    // ── Rejoining ─────────────────────────────────────────────────────────────

    /// <summary>MOD-128: a member who rejoins during a mute gets the role back.</summary>
    [Fact]
    public async Task RejoiningWhileMuted_GivesTheRoleBack()
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: false);
        await this.LeaveAndRejoinAsync(this.Guild.User1);

        var grant = await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
                                                                        && request.Path == $"guilds/{this.Guild.Id}/members/{this.Guild.User1.Id}/roles/{this._muteRoleId}");
        grant.AuditLogReason.ShouldBe("Rejoined while muted");
    }

    /// <summary>MOD-129, MOD-131 and MOD-133: no role after the mute ended, after an unmute, or with moderation off.</summary>
    [Theory]
    [InlineData("expired")]
    [InlineData("unmuted")]
    [InlineData("moderation off")]
    public async Task Rejoining_WithoutAnActiveMute_GivesNoRole(string situation)
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: situation == "expired");
        if (situation == "unmuted")
            (await this.Settings.RemoveMute(new UserId(this.Guild.User1.Id), this.GuildId, new ModeratorId(this.Guild.Moderator.Id), TestContext.Current.CancellationToken)).ShouldSucceed();
        if (situation == "moderation off")
            await this._bot.SetModuleAsync(Module.Moderation, false);

        await this.LeaveAndRejoinAsync(this.Guild.User1);

        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Put
                                                                    && request.Path.Contains("/roles/", StringComparison.Ordinal), NothingHappensWindow))
            .ShouldBeFalse();
    }

    /// <summary>MOD-132: when the recorded role is gone, the configured role is given instead.</summary>
    [Fact]
    public async Task RejoiningWhileMuted_WithTheRecordedRoleDeleted_GivesTheConfiguredRole()
    {
        await this.MuteByHandAsync(this.Guild.User1, expired: false);
        var configured = this._bot.Rest.CreateRole("Muted B");
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(configured), TestContext.Current.CancellationToken)).ShouldSucceed();
        this._bot.Rest.DeleteRole(this._muteRoleId);

        await this.LeaveAndRejoinAsync(this.Guild.User1);

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
                                                            && request.Path == $"guilds/{this.Guild.Id}/members/{this.Guild.User1.Id}/roles/{configured}");
    }

    /// <summary>
    ///     Records a mute the way <c>/Mute</c> would, without the command. An expired one is written straight to the
    ///     database, since the settings module refuses an end time in the past.
    /// </summary>
    private async Task MuteByHandAsync(FakeUser user, bool expired, ulong? roleId = null, GuildId? guildId = null, bool giveRole = true)
    {
        var role = roleId ?? this._muteRoleId;
        var now = DateTimeOffset.UtcNow;
        var mute = MuteAdded.Create(new UserId(user.Id), guildId ?? this.GuildId, new ModeratorId(this.Guild.Moderator.Id), new SinId(1),
            now.AddMinutes(-10), expired ? now.AddMinutes(-1) : now.AddMinutes(10), new RoleId(role)).ShouldSucceed();
        await using (var settingsContext = this._database.CreateSettingsDbContext())
        {
            settingsContext.Mutes.Add(mute);
            await settingsContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        if (giveRole)
            this._bot.SetRolesByHand(user, [.. this.Guild.GetMember(user).RoleIds, role]);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
    }

    private async Task LeaveAndRejoinAsync(FakeUser user)
    {
        this._bot.MemberLeaves(user);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        this._bot.MemberJoins(user);
    }

    /// <summary>Who recorded the mute's removal, or null while it isn't removed.</summary>
    private async Task<ulong?> RemovalByAsync()
    {
        await using var settingsContext = this._database.CreateSettingsDbContext();
        return (await settingsContext.Mutes.OfType<MuteRemoved>()
            .FirstOrDefaultAsync(mute => mute.UserId == new UserId(this.Guild.User1.Id)))?.ModeratorId.Value;
    }

    private Func<RecordedRequest, bool> IsRoleRevoke(FakeUser user)
        => request => request.Method == HttpMethod.Delete
                      && request.Path.StartsWith($"guilds/{this.Guild.Id}/members/{user.Id}/roles/", StringComparison.Ordinal);

    private Func<RecordedRequest, bool> IsDmTo(FakeUser user)
        => request => request.Method == HttpMethod.Post && request.Path == $"channels/{user.Id + 1_000}/messages";
}

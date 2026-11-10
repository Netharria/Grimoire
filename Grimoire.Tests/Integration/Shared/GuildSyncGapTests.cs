// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json.Nodes;
using DSharpPlus;
using DSharpPlus.Entities;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     The guild sync and invite cases <see cref="GuildSyncAndInviteTests" /> leaves out (backlog A-45): sections E
///     to H of <c>docs/testing/08-alerts-health.md</c>. Startup sync, the bot leaving and rejoining a guild, invite
///     tracking edge cases, and the history written when a member joins. User logs point at each guild's
///     <c>#modlog</c>.
/// </summary>
[Collection("Test collection")]
public sealed class GuildSyncGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private const ulong WithoutManageServer = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission
                                              | FakeGuild.EmbedLinksPermission | FakeGuild.ViewAuditLogPermission;

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild A => this._bot.Guild;
    private FakeGuild B => this._bot.SecondGuild!;
    private DiscordClient Client => this._bot.Services.GetRequiredService<DiscordClient>();
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await this._database.ResetDatabase();

    public async ValueTask DisposeAsync()
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
    }

    private async Task StartAsync(Action<FakeGuild>? configureGuild = null, bool secondGuild = false,
        Action<HostApplicationBuilder>? configureHost = null, bool waitForSync = true)
    {
        this._bot = await GrimoireTestBot.StartAsync(this._database, configureGuild, configureHost: configureHost,
            secondGuild: secondGuild);
        await this.ConfigureUserLogAsync(this.A);
        if (secondGuild)
            await this.ConfigureUserLogAsync(this.B);
        if (waitForSync)
            await this.WaitForHistoryAsync(secondGuild ? [this.A, this.B] : [this.A]);
    }

    private async Task ConfigureUserLogAsync(FakeGuild guild)
    {
        await this._bot.ConfigureModerationAsync(guild);
        await this._bot.SetModuleAsync(Module.UserLog, true, guild);
        var settings = this._bot.Services.GetRequiredService<SettingsModule>();
        foreach (var logType in new[]
                 {
                     GuildLogType.UserJoined, GuildLogType.UsernameUpdated, GuildLogType.NicknameUpdated,
                     GuildLogType.AvatarUpdated
                 })
            (await settings.SetLogChannelSetting(logType, new GuildId(guild.Id), new ModeratorId(guild.Owner.Id),
                new ChannelId(guild.ModLog.Id))).ShouldBeSetOrUnchanged();
    }

    /// <summary>
    ///     The sync saves history once, after storing the invites, so rows for every member of a guild mean its
    ///     invites are tracked too.
    /// </summary>
    private async Task WaitForHistoryAsync(params FakeGuild[] guilds)
    {
        foreach (var guild in guilds)
            (await GrimoireTestBot.EventuallyAsync(() => this.NicknameRowsAsync(guild), rows => rows >= guild.Members.Count))
                .ShouldBeGreaterThanOrEqualTo(guild.Members.Count, $"the sync should store history for guild {guild.Id}");
    }

    // ── E. Startup sync ───────────────────────────────────────────────────────

    /// <summary>
    ///     GLD-03: a username, nickname and avatar changed while the bot was off are stored at startup without
    ///     logging. The stored rows stand in for the old profile.
    /// </summary>
    [Fact]
    public async Task GLD03_ProfileChangesWhileTheBotWasOff_AreStoredSilentlyAtStartup()
    {
        await this.StartAsync();
        await this._bot.DisposeAsync();
        var user = new UserId(this.A.User1.Id);
        var guild = new GuildId(this.A.Id);
        await using (var dbContext = this._database.CreateDbContext())
        {
            await dbContext.UsernameHistory.Where(row => row.UserId == user).ExecuteDeleteAsync(Token);
            await dbContext.NicknameHistory.Where(row => row.UserId == user).ExecuteDeleteAsync(Token);
            await dbContext.Avatars.Where(row => row.UserId == user).ExecuteDeleteAsync(Token);
            var earlier = DateTimeOffset.UtcNow.AddMinutes(-5);
            dbContext.UsernameHistory.Add(new UsernameHistory { UserId = user, Username = Username.Create("oldname"), Timestamp = earlier });
            dbContext.NicknameHistory.Add(new NicknameHistory
            {
                UserId = user, GuildId = guild, Nickname = Nickname.Create("oldnick").ShouldSucceed(), Timestamp = earlier
            });
            dbContext.Avatars.Add(new Avatar
            {
                UserId = user, GuildId = guild, FileName = AvatarFileName.Create("https://cdn.discordapp.com/avatars/old.png"),
                Timestamp = earlier
            });
            await dbContext.SaveChangesAsync(Token);
        }

        await this.StartAsync(waitForSync: false);

        (await GrimoireTestBot.EventuallyAsync(() => this.RowCountsOfAsync(this.A.User1), counts => counts == (2, 2, 2)))
            .ShouldBe((2, 2, 2));
        (await this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                                    && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString()
                                                                        .EndsWith("Updated", StringComparison.Ordinal) == true,
            NothingHappensWindow)).ShouldBeFalse("the startup sync is silent");
    }

    /// <summary>
    ///     GLD-04 (KI-97): a member of two guilds whose username isn't stored should get one row. The second guild's
    ///     lookup can't see the first guild's unsaved row.
    /// </summary>
    [Fact(Skip = "Fails: the startup sync adds a UsernameHistory row per guild for a member of both. KI-97")]
    public async Task GLD04_AMemberOfTwoGuilds_GetsOneUsernameRowAtStartup()
    {
        await this.StartAsync(secondGuild: true);

        (await this.RowCountsOfAsync(this.A.User1)).Usernames.ShouldBe(1);
    }

    /// <summary>
    ///     GLD-05: a guild without Manage Server is synced with no invites, and the other guild is synced fully. The
    ///     bot doesn't ask for invites it can't list.
    /// </summary>
    [Fact]
    public async Task GLD05_AGuildWithoutManageServer_DoesNotStopTheOtherGuildsSync()
    {
        await this.StartAsync(guild => guild.BotPermissions = WithoutManageServer, secondGuild: true);

        this._bot.Rest.Requests.ShouldContain(request => request.Path == $"guilds/{this.B.Id}/invites");
        this._bot.Rest.Requests.ShouldNotContain(request => request.Path == $"guilds/{this.A.Id}/invites");
        // A join in each guild is logged, which needs each guild to be tracked.
        this.Leave(this.A, this.A.User1);
        this.Join(this.A, this.A.User1);
        Field(await this.WaitForJoinLogAsync(this.A, this.A.User1), "Invite Used").ShouldBe("Unknown Invite");
        this.Leave(this.B, this.B.User1);
        this.Join(this.B, this.B.User1);
        Field(await this.WaitForJoinLogAsync(this.B, this.B.User1), "Invite Used").ShouldBe("Unknown Invite");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     GLD-07 (KI-89): Discord failing one guild's invite list at startup should not stop the other guild's sync.
    ///     It does: nothing is saved and no guild is tracked.
    /// </summary>
    [Fact(Skip = "Fails: one failed invite list stops the whole startup sync, so no history is saved and no guild is tracked. KI-89")]
    public async Task GLD07_OneGuildsInviteListFailing_DoesNotStopTheOtherGuildsSync()
    {
        var failing = new GuildId(new FakeGuild(GrimoireTestBot.SecondGuildIdOffset).Id);
        var invite = new FakeInvite("works", new FakeGuild().Moderator, uses: 4);
        await this.StartAsync(guild => guild.Invites.Add(invite), secondGuild: true,
            configureHost: builder => builder.Services.AddHttpClient("DSharpPlus.Rest.HttpClient")
                .AddHttpMessageHandler(() => new FailInviteList(failing.Value)),
            waitForSync: false);

        await this.WaitForHistoryAsync(this.A);
        this._bot.MemberLeaves(this.A.User1);
        this._bot.MemberJoinsWithInvite(this.A.User1, invite);
        Field(await this.WaitForJoinLogAsync(this.A, this.A.User1), "Invite Used")
            .ShouldBe("https://discord.gg/works (5 uses)\n**Created By:** mod");
    }

    // ── F. Joining and leaving a guild ────────────────────────────────────────

    /// <summary>
    ///     GLD-20: re-adding the bot to a guild updates the status, stores the history missing for that guild and
    ///     tracks its invites, so a join there shows the invite used.
    /// </summary>
    [Fact]
    public async Task GLD20_ReaddingTheBot_SyncsTheGuild_AndTracksItsInvites()
    {
        await this.StartAsync(secondGuild: true);
        await this.RemoveBotFromBAsync();
        await this.ForgetHistoryAsync(this.B);
        var invite = new FakeInvite("readd", this.B.Admin, uses: 2);
        this.B.Invites.AddRange([new FakeInvite("other", this.B.Moderator, uses: 7), invite]);
        var statusesBefore = this.StatusesSaying("2 servers.");

        this._bot.Gateway.Dispatch("GUILD_CREATE", this.B.GuildCreatePayload());

        await this.WaitForHistoryAsync(this.B);
        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.StatusesSaying("2 servers.")),
            count => count > statusesBefore)).ShouldBeGreaterThan(statusesBefore);
        this.Leave(this.B, this.B.User1);
        this.Join(this.B, this.B.User1, invite);
        Field(await this.WaitForJoinLogAsync(this.B, this.B.User1), "Invite Used")
            .ShouldBe("https://discord.gg/readd (3 uses)\n**Created By:** admin");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>GLD-21 (KI-100): after the bot leaves a guild, the status should count one server.</summary>
    [Fact(Skip = "Fails: the \"Watching N servers.\" status isn't updated when the bot leaves a guild. KI-100")]
    public async Task GLD21_TheBotLeavingAGuild_UpdatesTheStatus()
    {
        await this.StartAsync(secondGuild: true);
        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.StatusesSaying("2 servers.")), count => count > 0))
            .ShouldBeGreaterThan(0);

        await this.RemoveBotFromBAsync();

        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.StatusesSaying("1 servers.")), count => count > 0,
            TimeSpan.FromSeconds(3))).ShouldBeGreaterThan(0);
    }

    /// <summary>
    ///     GLD-22: re-adding the bot without Manage Server raises no error, stores the history, and joins there show
    ///     "Unknown Invite".
    /// </summary>
    [Fact]
    public async Task GLD22_ReaddingTheBotWithoutManageServer_SyncsTheGuild_WithNoInvites()
    {
        await this.StartAsync(secondGuild: true);
        await this.RemoveBotFromBAsync();
        await this.ForgetHistoryAsync(this.B);
        this.B.BotPermissions = WithoutManageServer;
        var invite = new FakeInvite("unseen", this.B.Admin, uses: 2);
        this.B.Invites.Add(invite);
        var inviteRequestsBefore = this.InviteRequests(this.B);

        this._bot.Gateway.Dispatch("GUILD_CREATE", this.B.GuildCreatePayload());

        await this.WaitForHistoryAsync(this.B);
        this.Leave(this.B, this.B.User1);
        this.Join(this.B, this.B.User1, invite);
        Field(await this.WaitForJoinLogAsync(this.B, this.B.User1), "Invite Used").ShouldBe("Unknown Invite");
        this.InviteRequests(this.B).ShouldBe(inviteRequestsBefore);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     GLD-23 (KI-90): a guild synced without Manage Server is tracked with no invites. Once the bot gets Manage
    ///     Server, every listed invite looks new, and the join is blamed on whichever is listed first. Several unused
    ///     invites make the wrong pick likely.
    /// </summary>
    [Fact(Skip = "Fails: a guild tracked with no invites matches the join to whichever invite is listed first. KI-90")]
    public async Task GLD23_AfterGettingManageServer_AJoin_ShowsTheInviteUsed()
    {
        var used = new FakeInvite("used", new FakeGuild().Admin, uses: 1);
        await this.StartAsync(guild =>
        {
            guild.BotPermissions = WithoutManageServer;
            guild.Invites.AddRange([
                .. Enumerable.Range(1, 20).Select(i => new FakeInvite($"unused{i}", guild.Moderator, uses: i)), used
            ]);
        });
        this.A.BotPermissions = FakeGuild.AdministratorPermission;
        this._bot.Gateway.Dispatch("GUILD_ROLE_UPDATE", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(this.A.Id), ["role"] = this.A.RoleJsonOf(this.A.BotRoleId)
        });
        await GrimoireTestBot.EventuallyAsync(
            () => Task.FromResult(this.Client.Guilds[this.A.Id].CurrentMember.Permissions.HasPermission(DiscordPermission.ManageGuild)),
            hasIt => hasIt);
        this._bot.MemberLeaves(this.A.User1);

        this._bot.MemberJoinsWithInvite(this.A.User1, used);

        Field(await this.WaitForJoinLogAsync(this.A, this.A.User1), "Invite Used")
            .ShouldBe("https://discord.gg/used (2 uses)\n**Created By:** admin");
    }

    // ── G. Invites ────────────────────────────────────────────────────────────

    /// <summary>
    ///     GLD-42: an invite created while the bot runs is tracked, so a join with it shows it with one use. The other
    ///     new invites stay unused, which only tracking can tell apart.
    /// </summary>
    [Fact]
    public async Task GLD42_AnInviteCreatedWhileRunning_IsTracked_AndShownWhenUsed()
    {
        await this.StartAsync();
        var fresh = new FakeInvite("fresh", this.A.Admin);
        FakeInvite[] invites = [.. Enumerable.Range(1, 5).Select(i => new FakeInvite($"decoy{i}", this.A.Moderator)), fresh];
        foreach (var invite in invites)
        {
            this.A.Invites.Add(invite);
            this._bot.Gateway.Dispatch("INVITE_CREATE", this.InviteCreatedJson(invite));
        }

        await Task.Delay(TimeSpan.FromMilliseconds(300), Token);
        this._bot.MemberLeaves(this.A.User1);

        this._bot.MemberJoinsWithInvite(this.A.User1, fresh);

        Field(await this.WaitForJoinLogAsync(this.A, this.A.User1), "Invite Used")
            .ShouldBe("https://discord.gg/fresh (1 uses)\n**Created By:** admin");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>GLD-45: a join that matches no invite, in a guild with a vanity URL, is shown as the vanity invite.</summary>
    [Fact]
    public async Task GLD45_AJoinThroughTheVanityUrl_ShowsVanityInvite()
    {
        await this.StartAsync(guild =>
        {
            guild.VanityUrlCode = "grimoire";
            guild.Invites.Add(new FakeInvite("normal", guild.Admin, uses: 3));
        });
        this._bot.MemberLeaves(this.A.User1);

        this._bot.MemberJoins(this.A.User1);

        Field(await this.WaitForJoinLogAsync(this.A, this.A.User1), "Invite Used").ShouldBe("Vanity Invite");
    }

    /// <summary>
    ///     GLD-47: with View Audit Log, a revoked invite is forgotten, without a warning. Had it been kept, an unused
    ///     single-use invite that disappeared would be blamed for the next join that matches nothing.
    /// </summary>
    [Fact]
    public async Task GLD47_ARevokedInvite_IsForgotten()
    {
        var revoked = new FakeInvite("revoked", new FakeGuild().Admin, uses: 0, maxUses: 1);
        await this.StartAsync(guild => guild.Invites.Add(revoked));

        this.RevokeInvite(revoked);

        await this._bot.Rest.WaitForRequestAsync(request => request.Path == $"guilds/{this.A.Id}/audit-logs");
        await Task.Delay(TimeSpan.FromMilliseconds(300), Token);
        this._bot.MemberLeaves(this.A.User1);
        this._bot.MemberJoins(this.A.User1);
        Field(await this.WaitForJoinLogAsync(this.A, this.A.User1), "Invite Used").ShouldBe("Unknown Invite");
        this._bot.Logs.Events.ShouldNotContain(logEvent => logEvent.MessageTemplate.Text.StartsWith(
            "Was not able to retrieve audit log entry", StringComparison.Ordinal));
    }

    /// <summary>
    ///     GLD-49 (KI-98): without View Audit Log a revoked invite is kept, as if used up. A join through another
    ///     single-use invite should still name that one, but any of the vanished single-use invites can be blamed.
    ///     Several revoked invites make the wrong pick likely.
    /// </summary>
    [Fact(Skip = "Fails: a single-use invite kept after an unconfirmed revoke can be blamed for a join through another. KI-98")]
    public async Task GLD49_WithoutViewAuditLog_AJoinThroughASingleUseInvite_ShowsThatInvite()
    {
        var used = new FakeInvite("used", new FakeGuild().Admin, uses: 0, maxUses: 1);
        var revoked = Enumerable.Range(1, 5).Select(i => new FakeInvite($"revoked{i}", new FakeGuild().Moderator, 0, 1)).ToArray();
        await this.StartAsync(guild =>
        {
            guild.BotPermissions = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission
                                   | FakeGuild.EmbedLinksPermission | FakeGuild.ManageGuildPermission;
            guild.Invites.AddRange([.. revoked, used]);
        });
        foreach (var invite in revoked)
            this.RevokeInvite(invite);
        await GrimoireTestBot.EventuallyAsync(
            () => Task.FromResult(this._bot.Rest.Requests.Count(request => request.Path == $"guilds/{this.A.Id}/audit-logs")),
            count => count >= revoked.Length);
        this._bot.MemberLeaves(this.A.User1);

        this._bot.MemberJoinsWithInvite(this.A.User1, used);

        Field(await this.WaitForJoinLogAsync(this.A, this.A.User1), "Invite Used")
            .ShouldBe("https://discord.gg/used (1 uses)\n**Created By:** admin");
    }

    /// <summary>
    ///     GLD-50: an expired invite should be forgotten without an audit-log lookup. Discord's <c>INVITE_DELETE</c>
    ///     carries only the code, channel and guild, so the bot never sees an expiry.
    /// </summary>
    [Fact(Skip = "Fails: INVITE_DELETE has no expiry, so an expired invite always costs an audit-log lookup and a warning, and is kept. KI-108")]
    public async Task GLD50_AnExpiredInvite_IsForgottenWithoutAnAuditLookup()
    {
        var expiring = new FakeInvite("expiring", new FakeGuild().Admin, uses: 3);
        await this.StartAsync(guild => guild.Invites.Add(expiring));

        this.A.Invites.Remove(expiring);
        this._bot.Gateway.Dispatch("INVITE_DELETE", this.InviteDeletedJson(expiring));

        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Path == $"guilds/{this.A.Id}/audit-logs",
            NothingHappensWindow)).ShouldBeFalse();
        this._bot.Logs.Events.ShouldNotContain(logEvent => logEvent.MessageTemplate.Text.StartsWith(
            "Was not able to retrieve audit log entry", StringComparison.Ordinal));
    }

    // ── G and H. Joins with the User Log off ──────────────────────────────────

    /// <summary>
    ///     GLD-51 and GLD-64: with the User Log off, a join posts no log, but a rejoin after a username change still
    ///     stores the new username.
    /// </summary>
    [Fact]
    public async Task GLD51_GLD64_WithTheUserLogOff_AJoinIsNotLogged_ButHistoryIsStillAdded()
    {
        await this.StartAsync();
        await this._bot.SetModuleAsync(Module.UserLog, false);
        this.Leave(this.A, this.A.User1);

        this.Join(this.A, this.A.User1, username: "renamed");

        (await GrimoireTestBot.EventuallyAsync(() => this.UsernamesOfAsync(this.A.User1), names => names.Count == 2))
            .ShouldBe(["user1", "renamed"]);
        (await this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                                    && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString() == "User Joined",
            NothingHappensWindow)).ShouldBeFalse();
    }

    // ── H. Member joins ───────────────────────────────────────────────────────

    /// <summary>GLD-62: a member who changed username while away gets a new row on rejoin, without a username log.</summary>
    [Fact]
    public async Task GLD62_ARejoinAfterAUsernameChange_StoresIt_WithoutAUsernameLog()
    {
        await this.StartAsync();
        // The username handler ignores history newer than two seconds; wait so a stray log would show.
        await Task.Delay(TimeSpan.FromSeconds(2.5), Token);
        this.Leave(this.A, this.A.User1);

        this.Join(this.A, this.A.User1, username: "renamed");

        await this.WaitForJoinLogAsync(this.A, this.A.User1);
        (await GrimoireTestBot.EventuallyAsync(() => this.UsernamesOfAsync(this.A.User1), names => names.Count == 2))
            .ShouldBe(["user1", "renamed"]);
        (await this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                                    && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString() == "Username Updated",
            NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>GLD-63: a member who had a nickname loses it by leaving, so the rejoin stores a row with none.</summary>
    [Fact]
    public async Task GLD63_ARejoinAfterLosingANickname_StoresARowWithNoNickname()
    {
        await this.StartAsync();
        // Stands in for a nickname set before the bot started: it replaces the sync's empty-nickname row.
        await using (var seed = this._database.CreateDbContext())
        {
            await seed.NicknameHistory.Where(row => row.UserId == new UserId(this.A.User1.Id)).ExecuteDeleteAsync(Token);
            seed.NicknameHistory.Add(new NicknameHistory
            {
                UserId = new UserId(this.A.User1.Id), GuildId = new GuildId(this.A.Id),
                Nickname = Nickname.Create("One").ShouldSucceed(), Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
            });
            await seed.SaveChangesAsync(Token);
        }

        this.Leave(this.A, this.A.User1);
        this.Join(this.A, this.A.User1);

        var newest = await GrimoireTestBot.EventuallyAsync(this.NewestNicknameOfUser1Async, row => row is { Nickname: null },
            TimeSpan.FromSeconds(5));
        newest.ShouldNotBeNull().Nickname.ShouldBeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Fails <c>GET guilds/{id}/invites</c> for one guild, before the fake REST is reached.</summary>
    private sealed class FailInviteList(ulong guildId) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => request.Method == HttpMethod.Get
               && request.RequestUri!.AbsolutePath.EndsWith($"/guilds/{guildId}/invites", StringComparison.Ordinal)
                ? Task.FromResult(FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions"))
                : base.SendAsync(request, cancellationToken);
    }

    private async Task RemoveBotFromBAsync()
    {
        this._bot.BotLeavesGuild(this.B);
        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.Client.Guilds.ContainsKey(this.B.Id)), inGuild => !inGuild))
            .ShouldBeFalse("the bot should have left the guild");
    }

    private int StatusesSaying(string text)
        => this._bot.Gateway.SentByBot.Count(sent => sent["op"]?.GetValue<int>() == 3
                                                     && sent.ToJsonString().Contains(text, StringComparison.Ordinal));

    private int InviteRequests(FakeGuild guild)
        => this._bot.Rest.Requests.Count(request => request.Path == $"guilds/{guild.Id}/invites");

    private void RevokeInvite(FakeInvite invite)
    {
        this.A.Invites.Remove(invite);
        this._bot.Rest.AddInviteDeleteAuditLogEntry(this.A.Admin, invite.Code);
        this._bot.Gateway.Dispatch("INVITE_DELETE", this.InviteDeletedJson(invite));
    }

    private JsonObject InviteDeletedJson(FakeInvite invite) => new()
    {
        ["channel_id"] = FakeUser.Snowflake(this.A.General.Id),
        ["guild_id"] = FakeUser.Snowflake(this.A.Id),
        ["code"] = invite.Code
    };

    private JsonObject InviteCreatedJson(FakeInvite invite) => new()
    {
        ["channel_id"] = FakeUser.Snowflake(this.A.General.Id),
        ["guild_id"] = FakeUser.Snowflake(this.A.Id),
        ["code"] = invite.Code,
        ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
        ["inviter"] = invite.Inviter?.ToJson(),
        ["max_age"] = 0,
        ["max_uses"] = invite.MaxUses,
        ["temporary"] = false,
        ["uses"] = invite.Uses
    };

    /// <summary>A member leaves any guild, and the gateway tells the bot.</summary>
    private void Leave(FakeGuild guild, FakeUser user)
    {
        guild.Depart(user);
        this._bot.Gateway.Dispatch("GUILD_MEMBER_REMOVE", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(guild.Id), ["user"] = user.ToJson()
        });
    }

    /// <summary>A member joins any guild, optionally with an invite and under a new username.</summary>
    private void Join(FakeGuild guild, FakeUser user, FakeInvite? invite = null, string? username = null)
    {
        if (invite is not null)
        {
            invite.Uses++;
            if (invite.MaxUses > 0 && invite.Uses >= invite.MaxUses)
                guild.Invites.Remove(invite);
        }

        guild.Return(user);
        var member = FakeGuild.MemberJson(guild.GetMember(user));
        member["guild_id"] = FakeUser.Snowflake(guild.Id);
        member["joined_at"] = DateTimeOffset.UtcNow.ToString("O");
        if (username is not null)
        {
            member["user"]!["username"] = username;
            member["user"]!["global_name"] = username;
        }

        this._bot.Gateway.Dispatch("GUILD_MEMBER_ADD", member);
    }

    private async Task<JsonNode> WaitForJoinLogAsync(FakeGuild guild, FakeUser user)
        => (await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                                && request.Path == $"channels/{guild.ModLog.Id}/messages"
                                                                && request.Json?["embeds"]?[0] is { } embed
                                                                && embed["author"]?["name"]?.GetValue<string>() == "User Joined"
                                                                && FieldOrNull(embed, "Name") == user.MemberMention))
            .Json!["embeds"]![0]!;

    private async Task ForgetHistoryAsync(FakeGuild guild)
    {
        await using var dbContext = this._database.CreateDbContext();
        await dbContext.NicknameHistory.Where(row => row.GuildId == new GuildId(guild.Id)).ExecuteDeleteAsync(Token);
        await dbContext.Avatars.Where(row => row.GuildId == new GuildId(guild.Id)).ExecuteDeleteAsync(Token);
    }

    private async Task<int> NicknameRowsAsync(FakeGuild guild)
    {
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.NicknameHistory.CountAsync(row => row.GuildId == new GuildId(guild.Id), Token);
    }

    private async Task<NicknameHistory?> NewestNicknameOfUser1Async()
    {
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.NicknameHistory.AsNoTracking()
            .Where(row => row.UserId == new UserId(this.A.User1.Id) && row.GuildId == new GuildId(this.A.Id))
            .OrderByDescending(row => row.Timestamp).FirstOrDefaultAsync(Token);
    }

    private async Task<List<string>> UsernamesOfAsync(FakeUser user)
    {
        await using var dbContext = this._database.CreateDbContext();
        return [.. (await dbContext.UsernameHistory.AsNoTracking().Where(row => row.UserId == new UserId(user.Id))
                .OrderBy(row => row.Timestamp).ToListAsync(Token))
            .Select(row => row.Username.Value)];
    }

    private async Task<(int Usernames, int Nicknames, int Avatars)> RowCountsOfAsync(FakeUser user)
    {
        await using var dbContext = this._database.CreateDbContext();
        var userId = new UserId(user.Id);
        return (await dbContext.UsernameHistory.CountAsync(row => row.UserId == userId, Token),
            await dbContext.NicknameHistory.CountAsync(row => row.UserId == userId, Token),
            await dbContext.Avatars.CountAsync(row => row.UserId == userId, Token));
    }

    private static string Field(JsonNode embed, string name)
        => FieldOrNull(embed, name) ?? throw new ShouldAssertException($"The embed has no \"{name}\" field: {embed.ToJsonString()}");

    private static string? FieldOrNull(JsonNode embed, string name)
        => embed["fields"]?.AsArray().FirstOrDefault(field => field!["name"]!.GetValue<string>() == name)?["value"]?.GetValue<string>();
}

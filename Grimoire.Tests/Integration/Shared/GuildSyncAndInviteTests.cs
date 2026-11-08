// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     <c>GuildAdded</c>, <c>UpdateAllGuilds</c>, <c>MemberAdded</c> and invite tracking (backlog A-33). At startup
///     the bot stores every member's username, nickname and avatar and snapshots the invites. When someone joins, it
///     compares invite use counts to tell which invite they used. The user log points at <c>#modlog</c>.
/// </summary>
[Collection("Test collection")]
public sealed class GuildSyncAndInviteTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await this._database.ResetDatabase();

    public async ValueTask DisposeAsync()
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
    }

    private async Task StartAsync(Action<FakeGuild>? configureGuild = null)
    {
        this._bot = await GrimoireTestBot.StartAsync(this._database, configureGuild);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.UserLog, true);
        var settings = this._bot.Services.GetRequiredService<SettingsModule>();
        foreach (var logType in new[] { GuildLogType.UserJoined, GuildLogType.AvatarUpdated })
            (await settings.SetLogChannelSetting(logType, new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id),
                new ChannelId(this.Guild.ModLog.Id))).ShouldBeSetOrUnchanged();

        // Ready doesn't wait for the startup sync, which stores the invites and then the history. A join before then
        // has no invites to match against (KI-52).
        await GrimoireTestBot.EventuallyAsync(this.HistoryAsync,
            history => history.Usernames.Count >= this.Guild.Members.Count);
    }

    // ── Startup sync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Startup_StoresEveryMembersUsernameNicknameAndAvatar()
    {
        await this.StartAsync();

        var memberIds = this.Guild.Members.Select(member => new UserId(member.User.Id)).ToHashSet();
        var (usernames, nicknames, avatars) = await this.HistoryAsync();
        usernames.Select(row => row.UserId).ToHashSet().ShouldBe(memberIds, ignoreOrder: true);
        usernames.Single(row => row.UserId == new UserId(this.Guild.User1.Id)).Username.Value.ShouldBe("user1");
        nicknames.Select(row => row.UserId).ToHashSet().ShouldBe(memberIds, ignoreOrder: true);
        avatars.Select(row => row.UserId).ToHashSet().ShouldBe(memberIds, ignoreOrder: true);
    }

    /// <summary>
    ///     I-2: without Manage Server the bot can't list invites. That must not stop the startup sync, or the join
    ///     log, which then shows no invite. The bot doesn't ask for invites it can't list.
    /// </summary>
    [Fact]
    public async Task WithoutManageServer_StartupStillSyncs_AndJoinsAreLoggedWithNoInvite()
    {
        var invite = new FakeInvite("once", new FakeGuild().Admin, uses: 0, maxUses: 1);
        await this.StartAsync(guild =>
        {
            guild.BotPermissions = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission
                                   | FakeGuild.EmbedLinksPermission | FakeGuild.ViewAuditLogPermission;
            guild.Invites.Add(invite);
        });

        var memberIds = this.Guild.Members.Select(member => new UserId(member.User.Id)).ToHashSet();
        (await this.HistoryAsync()).Usernames.Select(row => row.UserId).ToHashSet().ShouldBe(memberIds, ignoreOrder: true);

        this._bot.MemberLeaves(this.Guild.User1);
        this._bot.MemberJoinsWithInvite(this.Guild.User1, invite);

        Field(await this.WaitForJoinLogAsync(), "Invite Used").ShouldBe("Unknown Invite");
        this._bot.Rest.Requests.ShouldNotContain(request => request.Path == $"guilds/{this.Guild.Id}/invites");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     I-10: without View Audit Log the bot can't tell who deleted an invite. That's a setup problem for the guild
    ///     to hear about, not an urgent alert for us. The invite is kept, as if used up.
    /// </summary>
    [Fact]
    public async Task AnInviteDeleted_WithoutViewAuditLog_IsReportedToTheGuild_NotRaisedAsAnError()
    {
        await this.StartAsync(guild => guild.BotPermissions = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission
                                                                | FakeGuild.EmbedLinksPermission | FakeGuild.ManageGuildPermission);
        await this._bot.ConfigureModerationAsync();

        this._bot.Gateway.Dispatch("INVITE_DELETE", new System.Text.Json.Nodes.JsonObject
        {
            ["channel_id"] = FakeUser.Snowflake(this.Guild.General.Id),
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["code"] = "gone"
        });

        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()?.Contains("audit log",
            StringComparison.OrdinalIgnoreCase) == true);
        this._bot.Logs.Events.ShouldNotContain(logEvent => logEvent.MessageTemplate.Text.StartsWith(
            "Unhandled exception in event handler", StringComparison.Ordinal));
    }

    /// <summary>Discord leaves out the inviter for some invites; the join log still shows the invite.</summary>
    [Fact]
    public async Task AJoin_WithAnInviteThatHasNoInviter_ShowsItsCreatorAsUnknown()
    {
        var invite = new FakeInvite("once", inviter: null, uses: 0, maxUses: 1);
        await this.StartAsync(guild => guild.Invites.Add(invite));
        this._bot.MemberLeaves(this.Guild.User1);

        this._bot.MemberJoinsWithInvite(this.Guild.User1, invite);

        var inviteUsed = Field(await this.WaitForJoinLogAsync(), "Invite Used");
        inviteUsed.ShouldStartWith("https://discord.gg/once ");
        inviteUsed.ShouldEndWith("\n**Created By:** Unknown");
    }

    /// <summary>Restarting on the same data adds no rows.</summary>
    [Fact]
    public async Task Restarting_DoesNotDuplicateHistory()
    {
        await this.StartAsync();
        var before = await this.HistoryAsync();
        await this._bot.DisposeAsync();

        await this.StartAsync();

        var after = await this.HistoryAsync();
        (after.Usernames.Count, after.Nicknames.Count, after.Avatars.Count)
            .ShouldBe((before.Usernames.Count, before.Nicknames.Count, before.Avatars.Count));
    }

    // ── Invites ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AJoin_ShowsTheInviteThatWasUsed()
    {
        var invite = new FakeInvite("abc123", new FakeGuild().Moderator, uses: 4);
        await this.StartAsync(guild => guild.Invites.AddRange([new FakeInvite("other", guild.Admin, uses: 9), invite]));
        this._bot.MemberLeaves(this.Guild.User1);

        this._bot.MemberJoinsWithInvite(this.Guild.User1, invite);

        Field(await this.WaitForJoinLogAsync(), "Invite Used").ShouldBe("https://discord.gg/abc123 (5 uses)\n**Created By:** mod");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>A single-use invite disappears when it's used; the bot works out it was that one.</summary>
    [Fact]
    public async Task AJoin_WithASingleUseInvite_ShowsIt()
    {
        var invite = new FakeInvite("once", new FakeGuild().Admin, uses: 0, maxUses: 1);
        await this.StartAsync(guild => guild.Invites.Add(invite));
        this._bot.MemberLeaves(this.Guild.User1);

        this._bot.MemberJoinsWithInvite(this.Guild.User1, invite);

        var inviteUsed = Field(await this.WaitForJoinLogAsync(), "Invite Used");
        inviteUsed.ShouldStartWith("https://discord.gg/once ");
        inviteUsed.ShouldEndWith("\n**Created By:** admin");
    }

    [Fact]
    public async Task AJoin_WithASingleUseInvite_CountsTheUse()
    {
        var invite = new FakeInvite("once", new FakeGuild().Admin, uses: 0, maxUses: 1);
        await this.StartAsync(guild => guild.Invites.Add(invite));
        this._bot.MemberLeaves(this.Guild.User1);

        this._bot.MemberJoinsWithInvite(this.Guild.User1, invite);

        Field(await this.WaitForJoinLogAsync(), "Invite Used").ShouldStartWith("https://discord.gg/once (1 uses)");
    }

    /// <summary>Two joins in a row are each matched to their own invite.</summary>
    [Fact]
    public async Task ConsecutiveJoins_AreMatchedToTheirOwnInvites()
    {
        var first = new FakeInvite("first", new FakeGuild().Moderator);
        var second = new FakeInvite("second", new FakeGuild().Admin);
        await this.StartAsync(guild => guild.Invites.AddRange([first, second]));
        this._bot.MemberLeaves(this.Guild.User1);
        this._bot.MemberLeaves(this.Guild.Higher);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Token);

        this._bot.MemberJoinsWithInvite(this.Guild.User1, first);
        Field(await this.WaitForJoinLogAsync(this.Guild.User1), "Invite Used").ShouldStartWith("https://discord.gg/first ");
        this._bot.MemberJoinsWithInvite(this.Guild.Higher, second);

        Field(await this.WaitForJoinLogAsync(this.Guild.Higher), "Invite Used").ShouldStartWith("https://discord.gg/second ");
    }

    // ── MemberAdded ───────────────────────────────────────────────────────────

    /// <summary>A rejoining member's next profile event shouldn't read as an avatar change.</summary>
    [Fact]
    public async Task ARejoin_DoesNotCauseAFalseAvatarChange()
    {
        await this.StartAsync();
        this._bot.MemberLeaves(this.Guild.User1);
        this._bot.MemberJoins(this.Guild.User1);
        await this.WaitForJoinLogAsync();

        this._bot.SetRolesByHand(this.Guild.User1, this.Guild.KickerRoleId);

        (await this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                                    && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString() == "Avatar Updated",
            NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>A member the bot has never seen gets history rows when they join.</summary>
    [Fact]
    public async Task ANewMember_GetsHistoryWhenTheyJoin()
    {
        await this.StartAsync();
        this._bot.MemberLeaves(this.Guild.User1);
        await using (var dbContext = this._database.CreateDbContext())
            await dbContext.UsernameHistory.Where(row => row.UserId == new UserId(this.Guild.User1.Id)).ExecuteDeleteAsync(Token);

        this._bot.MemberJoins(this.Guild.User1);

        (await GrimoireTestBot.EventuallyAsync(async () => (await this.HistoryAsync()).Usernames
                .Count(row => row.UserId == new UserId(this.Guild.User1.Id)), count => count == 1))
            .ShouldBe(1);
    }

    private async Task<JsonNode> WaitForJoinLogAsync(FakeUser? user = null)
        => await this._bot.WaitForModLogEmbedAsync(embed => embed["author"]?["name"]?.GetValue<string>() == "User Joined"
                                                            && Field(embed, "Name") == (user ?? this.Guild.User1).MemberMention);

    private async Task<(List<UsernameHistory> Usernames, List<NicknameHistory> Nicknames, List<Avatar> Avatars)> HistoryAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return (await dbContext.UsernameHistory.AsNoTracking().ToListAsync(Token),
            await dbContext.NicknameHistory.AsNoTracking().ToListAsync(Token),
            await dbContext.Avatars.AsNoTracking().ToListAsync(Token));
    }

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

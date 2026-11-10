// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json.Nodes;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>
///     The user-log cases <see cref="UserLogTests" /> leaves out (backlog A-45): sections J and K of
///     <c>docs/testing/05-logging.md</c>. Every user log points at the guild's <c>#modlog</c> unless a case says
///     otherwise.
/// </summary>
[Collection("Test collection")]
public sealed class UserLogEventGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private static readonly GuildLogType[] UserLogTypes =
        [GuildLogType.UserJoined, GuildLogType.UserLeft, GuildLogType.UsernameUpdated, GuildLogType.NicknameUpdated, GuildLogType.AvatarUpdated];

    // GrimoireColor.Green, Yellow and Red as Discord receives them.
    private const int Green = 0x008000;
    private const int Yellow = 0xFFBA08;
    private const int Red = 0xA30000;

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await database.ResetDatabase();

    public async ValueTask DisposeAsync()
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
    }

    private async Task StartAsync(Action<FakeGuild>? configureGuild = null, bool secondGuild = false)
    {
        this._bot = await GrimoireTestBot.StartAsync(database, configureGuild, secondGuild: secondGuild);
        await this.ConfigureUserLogAsync(this.Guild);
        if (this._bot.SecondGuild is { } second)
            await this.ConfigureUserLogAsync(second);
        // The startup sync stores the invites and then the history. A join before then has no invites to match.
        await GrimoireTestBot.EventuallyAsync(this.NicknameRowsAsync,
            rows => rows >= this.Guild.Members.Count * (secondGuild ? 2 : 1));
    }

    private async Task ConfigureUserLogAsync(FakeGuild guild)
    {
        await this._bot.ConfigureModerationAsync(guild);
        await this._bot.SetModuleAsync(Module.UserLog, true, guild);
        foreach (var logType in UserLogTypes)
            await this.PointLogAtAsync(logType, guild.ModLog, guild);
    }

    private async Task PointLogAtAsync(GuildLogType logType, FakeChannel? channel, FakeGuild? guild = null)
    {
        guild ??= this.Guild;
        (await this.Settings.SetLogChannelSetting(logType, new GuildId(guild.Id), new ModeratorId(guild.Owner.Id),
            channel is null ? null : new ChannelId(channel.Id))).ShouldBeSetOrUnchanged();
    }

    // ── J. Join and leave ─────────────────────────────────────────────────────

    /// <summary>LOG-140: the invite's count after the join, its creator, and a green embed for an old account.</summary>
    [Fact]
    public async Task LOG140_AJoin_ShowsTheInviteWithItsNewCountAndCreator_InGreen()
    {
        var invite = new FakeInvite("twice", new FakeGuild().Admin, uses: 2);
        await this.StartAsync(guild => guild.Invites.Add(invite));
        this._bot.MemberLeaves(this.Guild.User1);

        this._bot.MemberJoinsWithInvite(this.Guild.User1, invite);

        var embed = await this.WaitForLogAsync("User Joined", this.Guild.User1);
        Field(embed, "Invite Used").ShouldBe("https://discord.gg/twice (3 uses)\n**Created By:** admin");
        embed["color"]!.GetValue<int>().ShouldBe(Green);
        FieldOrNull(embed, "New Account").ShouldBeNull();
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     LOG-142 (KI-67): without Manage Server the bot can't tell which invite was used, so the join log should
    ///     say so. It reads "Vanity Invite" instead whenever the guild has a vanity URL.
    /// </summary>
    [Fact(Skip = "Fails: an unknown invite reads \"Vanity Invite\" whenever the guild has a vanity URL, even without Manage Server. KI-67")]
    public async Task LOG142_WithoutManageServer_AVanityGuildsJoin_IsAnUnknownInvite()
    {
        var invite = new FakeInvite("normal", new FakeGuild().Admin, uses: 1);
        await this.StartAsync(guild =>
        {
            guild.VanityUrlCode = "grimoire";
            guild.BotPermissions = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission
                                   | FakeGuild.EmbedLinksPermission | FakeGuild.ViewAuditLogPermission;
            guild.Invites.Add(invite);
        });
        this._bot.MemberLeaves(this.Guild.User1);

        this._bot.MemberJoinsWithInvite(this.Guild.User1, invite);

        Field(await this.WaitForLogAsync("User Joined", this.Guild.User1), "Invite Used").ShouldBe("Unknown Invite");
    }

    /// <summary>LOG-143: an account under 7 days old makes the embed yellow and adds a "New Account" field.</summary>
    [Fact]
    public async Task LOG143_ANewAccountsJoin_IsYellow_WithANewAccountField()
    {
        await this.StartAsync();
        // A snowflake minted now dates the account to now.
        var newcomer = new FakeUser(this._bot.NextSnowflake(), "newcomer");
        this._bot.Gateway.Dispatch("GUILD_MEMBER_ADD", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["user"] = newcomer.ToJson(),
            ["nick"] = null,
            ["avatar"] = null,
            ["roles"] = new JsonArray(),
            ["joined_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["deaf"] = false,
            ["mute"] = false,
            ["flags"] = 0,
            ["pending"] = false
        });

        var embed = await this.WaitForLogAsync("User Joined", newcomer);
        embed["color"]!.GetValue<int>().ShouldBe(Yellow);
        Field(embed, "New Account").ShouldStartWith("Created <t:");
    }

    /// <summary>LOG-147: a member with no roles leaves with "Roles[0]": "None", in red.</summary>
    [Fact]
    public async Task LOG147_AMemberWithNoRoles_LeavesWithRolesNone()
    {
        await this.StartAsync();

        this._bot.MemberLeaves(this.Guild.User1);

        var embed = await this.WaitForLogAsync("User Left", this.Guild.User1);
        Field(embed, "Roles[0]").ShouldBe("None");
        embed["color"]!.GetValue<int>().ShouldBe(Red);
    }

    /// <summary>LOG-148: a kick shows as a plain leave, with nothing marking it as a kick.</summary>
    [Fact]
    public async Task LOG148_AKick_IsLoggedAsAPlainLeave()
    {
        await this.StartAsync();

        this._bot.Rest.AddAuditLogEntry(FakeDiscordRest.KickAuditAction, this.Guild.Moderator, this.Guild.User1.Id, "rude");
        this._bot.MemberLeaves(this.Guild.User1);

        var embed = await this.WaitForLogAsync("User Left", this.Guild.User1);
        embed["fields"]!.AsArray().Select(field => field!["name"]!.GetValue<string>())
            .ShouldBe(["Name", "Created", "Joined", "Roles[0]"]);
        embed.ToJsonString().ShouldNotContain("kick", Case.Insensitive);
    }

    /// <summary>LOG-149: with only the join log off, a join posts nothing while a leave still logs.</summary>
    [Fact]
    public async Task LOG149_WithTheJoinLogOff_AJoinPostsNothing()
    {
        await this.StartAsync();
        await this.PointLogAtAsync(GuildLogType.UserJoined, null);

        this._bot.MemberLeaves(this.Guild.User1);
        await this.WaitForLogAsync("User Left", this.Guild.User1);
        this._bot.MemberJoins(this.Guild.User1);

        (await this.LoggedWithinAsync("User Joined", NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>LOG-151: a join log in a channel the bot can't post in is reported in the mod log.</summary>
    [Fact]
    public async Task LOG151_AJoinLogTheBotCannotPostIn_IsReportedInTheModLog()
    {
        await this.StartAsync(guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this.PointLogAtAsync(GuildLogType.UserJoined, this.Guild.Private);
        this._bot.MemberLeaves(this.Guild.User1);

        this._bot.MemberJoins(this.Guild.User1);

        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()?.StartsWith(
            $"I could not post in {this.Guild.Private.Mention}.", StringComparison.Ordinal) == true);
    }

    // ── K. Nickname, username and avatar changes ──────────────────────────────

    /// <summary>LOG-162: clearing a nickname logs Before "Uno", After "`None`", and stores a row with no nickname.</summary>
    [Fact]
    public async Task LOG162_ClearingANickname_IsLoggedAsNone_AndStored()
    {
        await this.StartAsync();
        await this.SeedNicknameAsync(this.Guild.User1, "Uno");

        this._bot.UpdateProfile(this.Guild.User1, nickname: "");

        var embed = await this.WaitForLogAsync("Nickname Updated");
        Field(embed, "Before").ShouldBe("Uno");
        Field(embed, "After").ShouldBe("`None`");
        var newest = await GrimoireTestBot.EventuallyAsync(() => this.NewestNicknameAsync(this.Guild.User1),
            row => row is { Nickname: null });
        newest.ShouldNotBeNull().Nickname.ShouldBeNull();
    }

    /// <summary>LOG-163: a change of case only is not a nickname change.</summary>
    [Fact]
    public async Task LOG163_ACaseOnlyNicknameChange_IsNeitherLoggedNorStored()
    {
        await this.StartAsync();
        await this.SeedNicknameAsync(this.Guild.User1, "Uno");
        var before = await this.NicknameRowsAsync();

        this._bot.UpdateProfile(this.Guild.User1, nickname: "UNO");

        (await this.LoggedWithinAsync("Nickname Updated", NothingHappensWindow)).ShouldBeFalse();
        (await this.NicknameRowsAsync()).ShouldBe(before);
    }

    /// <summary>
    ///     LOG-166 (KI-68): usernames are global, so one change should be logged in every guild the member shares with
    ///     the bot, and stored once. Each guild's update writes its own row.
    /// </summary>
    [Fact(Skip = "Fails: one username change writes a UsernameHistory row per guild. KI-68")]
    public async Task LOG166_AUsernameChangeSeenInTwoGuilds_IsLoggedInBoth_AndStoredOnce()
    {
        await this.StartAsync(secondGuild: true);
        // The handler ignores history newer than two seconds, to skip the rows the startup sync just wrote.
        await Task.Delay(TimeSpan.FromSeconds(2.5), Token);

        this.UpdateUser(this.Guild, this.Guild.User1, "renamed", "renamed");
        this.UpdateUser(this._bot.SecondGuild!, this.Guild.User1, "renamed", "renamed");

        await this.WaitForLogAsync("Username Updated", guild: this.Guild);
        await this.WaitForLogAsync("Username Updated", guild: this._bot.SecondGuild);
        await Task.Delay(TimeSpan.FromMilliseconds(500), Token);
        (await this.UsernamesOfAsync(this.Guild.User1)).ShouldBe(["user1", "renamed"]);
    }

    /// <summary>
    ///     LOG-166 (KI-68): the second guild's update can arrive later, for example after a reconnect. Its guild should
    ///     still be told. The first guild's new row already matches, so nothing is logged there.
    /// </summary>
    [Fact(Skip = "Fails: a guild whose update arrives more than 2 seconds after the first logs nothing. KI-68")]
    public async Task LOG166_AUsernameChangeReachingTheSecondGuildLater_IsStillLoggedThere()
    {
        await this.StartAsync(secondGuild: true);
        await Task.Delay(TimeSpan.FromSeconds(2.5), Token);

        this.UpdateUser(this.Guild, this.Guild.User1, "renamed", "renamed");
        await this.WaitForLogAsync("Username Updated", guild: this.Guild);
        await Task.Delay(TimeSpan.FromSeconds(2.5), Token);
        this.UpdateUser(this._bot.SecondGuild!, this.Guild.User1, "renamed", "renamed");

        var embed = await this.WaitForLogAsync("Username Updated", guild: this._bot.SecondGuild);
        Field(embed, "Before").ShouldBe("user1");
        Field(embed, "After").ShouldBe("renamed");
    }

    /// <summary>LOG-167: display names (global names) aren't tracked, so changing only that does nothing.</summary>
    [Fact]
    public async Task LOG167_ADisplayNameOnlyChange_IsNeitherLoggedNorStored()
    {
        await this.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(2.5), Token);

        this.UpdateUser(this.Guild, this.Guild.User1, "user1", "Shiny Display Name");

        (await this.LoggedWithinAsync("Username Updated", NothingHappensWindow)).ShouldBeFalse();
        (await this.UsernamesOfAsync(this.Guild.User1)).ShouldBe(["user1"]);
    }

    /// <summary>
    ///     LOG-173: when Discord refuses the avatar log's post (403, as for a channel without Attach Files), the guild
    ///     hears about it in the mod log, and the avatar is still stored.
    /// </summary>
    [Fact]
    public async Task LOG173_AnAvatarLogDiscordRefuses_IsReportedInTheModLog_AndTheAvatarIsStored()
    {
        const string hash = "0123456789abcdef0123456789abcdef";
        await this.StartAsync();
        await this.PointLogAtAsync(GuildLogType.AvatarUpdated, this.Guild.General);
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Guild.General.Id}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions"));

        this._bot.UpdateProfile(this.Guild.User1, avatarHash: hash);

        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()?.StartsWith(
            $"I could not post in {this.Guild.General.Mention}.", StringComparison.Ordinal) == true);
        await using var dbContext = database.CreateDbContext();
        (await dbContext.Avatars.Where(row => row.UserId == new UserId(this.Guild.User1.Id)).ToListAsync(Token))
            .ShouldContain(row => row.FileName.Value.Contains(hash));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>A <c>GUILD_MEMBER_UPDATE</c> in any guild with a new username and display (global) name.</summary>
    private void UpdateUser(FakeGuild guild, FakeUser user, string username, string globalName)
    {
        var member = FakeGuild.MemberJson(guild.GetMember(user));
        member["guild_id"] = FakeUser.Snowflake(guild.Id);
        member["user"]!["username"] = username;
        member["user"]!["global_name"] = globalName;
        this._bot.Gateway.Dispatch("GUILD_MEMBER_UPDATE", member);
    }

    /// <summary>
    ///     Stands in for a member who had a nickname when the bot started: an older row holding the nickname replaces
    ///     the startup sync's empty-nickname row, so rows written later sort after it.
    /// </summary>
    private async Task SeedNicknameAsync(FakeUser user, string nickname)
    {
        await using var seed = database.CreateDbContext();
        await seed.NicknameHistory.Where(row => row.UserId == new UserId(user.Id) && row.GuildId == new GuildId(this.Guild.Id))
            .ExecuteDeleteAsync(Token);
        seed.NicknameHistory.Add(new NicknameHistory
        {
            UserId = new UserId(user.Id),
            GuildId = new GuildId(this.Guild.Id),
            Nickname = Nickname.Create(nickname).ShouldSucceed(),
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await seed.SaveChangesAsync(Token);
    }

    private async Task<int> NicknameRowsAsync()
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.NicknameHistory.CountAsync(Token);
    }

    private async Task<NicknameHistory?> NewestNicknameAsync(FakeUser user)
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.NicknameHistory.AsNoTracking()
            .Where(row => row.UserId == new UserId(user.Id) && row.GuildId == new GuildId(this.Guild.Id))
            .OrderByDescending(row => row.Timestamp).FirstOrDefaultAsync(Token);
    }

    private async Task<List<string>> UsernamesOfAsync(FakeUser user)
    {
        await using var dbContext = database.CreateDbContext();
        return [.. (await dbContext.UsernameHistory.AsNoTracking().Where(row => row.UserId == new UserId(user.Id))
                .OrderBy(row => row.Timestamp).ToListAsync(Token))
            .Select(row => row.Username.Value)];
    }

    private bool IsLogPost(RecordedRequest request, FakeGuild guild)
        => request.Method == HttpMethod.Post && request.Path == $"channels/{guild.ModLog.Id}/messages";

    private async Task<JsonNode> WaitForLogAsync(string title, FakeUser? user = null, FakeGuild? guild = null)
        => (await this._bot.Rest.WaitForRequestAsync(request => this.IsLogPost(request, guild ?? this.Guild)
                                                                && request.Json?["embeds"]?[0] is { } embed
                                                                && embed["author"]?["name"]?.GetValue<string>() == title
                                                                && (user is null || FieldOrNull(embed, "Name") == user.MemberMention)))
            .Json!["embeds"]![0]!;

    private Task<bool> LoggedWithinAsync(string title, TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => this.IsLogPost(request, this.Guild)
                                                               && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString() == title,
            window);

    private static string Field(JsonNode embed, string name)
        => FieldOrNull(embed, name) ?? throw new ShouldAssertException($"The embed has no \"{name}\" field: {embed.ToJsonString()}");

    private static string? FieldOrNull(JsonNode embed, string name)
        => embed["fields"]?.AsArray().FirstOrDefault(field => field!["name"]!.GetValue<string>() == name)?["value"]?.GetValue<string>();
}

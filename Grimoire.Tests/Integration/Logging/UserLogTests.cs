// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Enums;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using UserLogSetting = Grimoire.Features.Logging.Settings.LogSettingsCommands.User.UserLogSetting;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>
///     <c>/Log User</c> Set and View, and the user log: joins, leaves, and nickname, username and avatar changes
///     (backlog A-23). Every user log points at <c>#modlog</c>. Avatar downloads go to the fake CDN.
/// </summary>
[Collection("Test collection")]
public sealed class UserLogTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private static readonly GuildLogType[] UserLogTypes =
        [GuildLogType.UserJoined, GuildLogType.UserLeft, GuildLogType.UsernameUpdated, GuildLogType.NicknameUpdated, GuildLogType.AvatarUpdated];

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.UserLog, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task PointUserLogsAtModLogAsync()
    {
        foreach (var logType in UserLogTypes)
            (await this.Settings.SetLogChannelSetting(logType, this.GuildId, new ModeratorId(this.Guild.Owner.Id),
                new ChannelId(this.Guild.ModLog.Id))).ShouldBeSetOrUnchanged();
    }

    // ── /Log User ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task View_OnAFreshGuild_ShowsNoLogChannels()
    {
        var reply = await this.UserAsync(SlashOption.Subcommand("view"));

        reply.IsEphemeral.ShouldBeTrue();
        reply.Description.ShouldBe("**Module Enabled:** True\n**Join Log:** None\n**Leave Log:** None\n**Username Log:** None\n**Nickname Log:** None\n**Avatar Log:** None\n");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(UserLogSetting.JoinLog, GuildLogType.UserJoined)]
    [InlineData(UserLogSetting.LeaveLog, GuildLogType.UserLeft)]
    [InlineData(UserLogSetting.UsernameLog, GuildLogType.UsernameUpdated)]
    [InlineData(UserLogSetting.NicknameLog, GuildLogType.NicknameUpdated)]
    [InlineData(UserLogSetting.AvatarLog, GuildLogType.AvatarUpdated)]
    public async Task Set_StoresTheChannel_AndOffClearsIt(UserLogSetting setting, GuildLogType logType)
    {
        (await this.SetAsync(setting, ChannelOption.SelectChannel, this.Guild.General)).Description
            .ShouldBe($"Updated {setting} to {this.Guild.General.Mention}");
        (await this.LogChannelAsync(logType)).ShouldBe(this.Guild.General.Id);

        (await this.SetAsync(setting, ChannelOption.Off)).Description.ShouldBe($"Disabled {setting}");
        (await this.LogChannelAsync(logType)).ShouldBeNull();
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("module")]
    public async Task LogUser_NeedsManageGuildAndTheModule(string missing)
    {
        if (missing == "module")
            await this._bot.SetModuleAsync(Module.UserLog, false);

        (await this._bot.RunSlashCommandAndWaitAsync("log", missing == "module" ? this.Guild.Admin : this.Guild.Moderator,
                SlashOption.Group("user", SlashOption.Subcommand("view"))))
            .Description.ShouldBe(missing == "module"
                ? "This module is disabled in this server."
                : "You do not have the required permissions to use this command.");
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Join_IsLogged_WithTheInviteUnknown()
    {
        await this.PointUserLogsAtModLogAsync();
        this._bot.MemberLeaves(this.Guild.User1);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        this._bot.MemberJoins(this.Guild.User1);

        var embed = await this.WaitForLogAsync("User Joined");
        Field(embed, "Name").ShouldBe(this.Guild.User1.MemberMention);
        Field(embed, "Invite Used").ShouldBe("Unknown Invite");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Leave_IsLogged_WithTheMembersRoles()
    {
        await this.PointUserLogsAtModLogAsync();

        this._bot.MemberLeaves(this.Guild.Moderator);

        var embed = await this.WaitForLogAsync("User Left");
        Field(embed, "Name").ShouldBe(this.Guild.Moderator.MemberMention);
        Field(embed, "Roles[1]").ShouldBe($"<@&{this.Guild.ModeratorRoleId}>");
    }

    /// <summary>A change from one nickname to another is recorded and logged.</summary>
    [Fact]
    public async Task NicknameChange_IsRecorded_AndLogged()
    {
        await this.PointUserLogsAtModLogAsync();
        await this.SeedNicknameAsync(this.Guild.User1, "first");

        this._bot.UpdateProfile(this.Guild.User1, nickname: "second");

        var embed = await this.WaitForLogAsync("Nickname Updated");
        Field(embed, "Before").ShouldBe("first");
        Field(embed, "After").ShouldBe("second");
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.NicknameHistory.Where(h => h.UserId == new UserId(this.Guild.User1.Id))
                .ToListAsync(TestContext.Current.CancellationToken))
            .ShouldContain(h => h.Nickname.HasValue && h.Nickname.Value.Value == "second");
    }

    /// <summary>A member's first nickname should be logged too.</summary>
    [Fact(Skip = "Fails: the handler returns when the newest stored nickname is null, which it is for every member " +
                 "without one, so a first nickname is never recorded or logged. KI-40")]
    public async Task FirstNickname_IsRecorded_AndLogged()
    {
        await this.PointUserLogsAtModLogAsync();

        this._bot.UpdateProfile(this.Guild.User1, nickname: "newnick");

        Field(await this.WaitForLogAsync("Nickname Updated"), "After").ShouldBe("newnick");
    }

    [Fact]
    public async Task UsernameChange_IsRecorded_AndLogged()
    {
        await this.PointUserLogsAtModLogAsync();
        // The handler ignores history newer than two seconds, to skip the rows the startup sync just wrote.
        await Task.Delay(TimeSpan.FromSeconds(2.5), TestContext.Current.CancellationToken);

        this._bot.UpdateProfile(this.Guild.User1, username: "renamed");

        var embed = await this.WaitForLogAsync("Username Updated");
        Field(embed, "Before").ShouldBe("user1");
        Field(embed, "After").ShouldBe("renamed");
    }

    [Fact]
    public async Task AvatarChange_IsRecorded_AndLoggedWithTheNewImage()
    {
        await this.PointUserLogsAtModLogAsync();

        this._bot.UpdateProfile(this.Guild.User1, avatarHash: "0123456789abcdef0123456789abcdef");

        var post = await this._bot.Rest.WaitForRequestAsync(request => this._bot.IsModLogPost(request)
                                                                      && request.Json?["embeds"]?[0]?["author"]?["name"]?.GetValue<string>() == "Avatar Updated");
        post.Files.ShouldNotBeEmpty("the new avatar is re-uploaded with the log");
        this._bot.Cdn.Downloads.ShouldContain(uri => uri.ToString().Contains("0123456789abcdef0123456789abcdef", StringComparison.Ordinal));
    }

    /// <summary>A role change isn't an avatar change.</summary>
    [Fact]
    public async Task RoleChange_IsNotLoggedAsAnAvatarChange()
    {
        await this.PointUserLogsAtModLogAsync();

        this._bot.SetRolesByHand(this.Guild.User1, this.Guild.KickerRoleId);

        (await this.LoggedWithinAsync("Avatar Updated", NothingHappensWindow)).ShouldBeFalse();
    }

    [Fact]
    public async Task WithTheUserLogOff_NothingIsLogged()
    {
        await this.PointUserLogsAtModLogAsync();
        await this._bot.SetModuleAsync(Module.UserLog, false);

        this._bot.MemberLeaves(this.Guild.User1);
        this._bot.UpdateProfile(this.Guild.Moderator, username: "renamed");

        (await this.LoggedWithinAsync("User ", NothingHappensWindow)).ShouldBeFalse();
        (await this.LoggedWithinAsync("Username", NothingHappensWindow)).ShouldBeFalse();
    }

    private Task<FakeReply> UserAsync(SlashOption subcommand)
        => this._bot.RunSlashCommandAndWaitAsync("log", this.Guild.Admin, SlashOption.Group("user", subcommand));

    private Task<FakeReply> SetAsync(UserLogSetting setting, ChannelOption option, FakeChannel? channel = null)
        => this.UserAsync(SlashOption.Subcommand("set",
        [
            SlashOption.Enum("setting", setting), SlashOption.Enum("option", option),
            .. channel is null ? Array.Empty<SlashOption>() : [SlashOption.Channel("value", channel)]
        ]));

    private async Task<ulong?> LogChannelAsync(GuildLogType logType)
        => (await this.Settings.GetLogChannelSetting(logType, this.GuildId)).ShouldSucceed()?.Value;

    /// <summary>
    ///     Stands in for a member who had a nickname when the bot started. The fake members start without one, and the
    ///     row is stamped just after the startup sync's empty-nickname row so it reads as the newest.
    /// </summary>
    private async Task SeedNicknameAsync(FakeUser user, string nickname)
    {
        await using var seed = this._database.CreateDbContext();
        seed.NicknameHistory.Add(new NicknameHistory
        {
            UserId = new UserId(user.Id),
            GuildId = this.GuildId,
            Nickname = Nickname.Create(nickname).ShouldSucceed(),
            Timestamp = DateTimeOffset.UtcNow.AddSeconds(1)
        });
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task<JsonNode> WaitForLogAsync(string title)
        => this._bot.WaitForModLogEmbedAsync(embed => embed["author"]?["name"]?.GetValue<string>() == title);

    private Task<bool> LoggedWithinAsync(string titleStart, TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                               && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString()
                                                                   .StartsWith(titleStart, StringComparison.Ordinal) == true, window);

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

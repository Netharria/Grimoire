// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Grimoire.Features.Shared.SharedDtos;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Channel-level permissions, section D of <c>docs/testing/09-cross-cutting.md</c> (XC-54 to XC-62), plus GEN-143
///     and MOD-64. Interactions carry permissions resolved for their channel (harness H-13), as on Discord. Here
///     <c>#general</c> stands in for the doc's <c>#spam</c>: <c>user1</c> holds Manage Messages there through a member
///     overwrite, and nowhere else. <c>/Warn</c> being channel-level is intended, so channel-only moderators can warn
///     (KI-17).
/// </summary>
[Collection("Test collection")]
public sealed class ChannelPermissionTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string PermissionError = "You do not have the required permissions to use this command.";
    private const string ModuleDisabledError = "This module is disabled in this server.";
    private const string BotPermissionError = "The bot did not have the needed permissions to execute this command.";
    private const int MemberOverwrite = 1;

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database, guild =>
            guild.SetOverwrite(guild.General.Id, guild.User1.Id, MemberOverwrite, FakeGuild.ManageMessagesPermission, 0));
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>H-13: Manage Messages in #general and its thread, not in #news; the owner and Administrator get all.</summary>
    [Fact]
    public void Harness_ResolvesThePermissionsForTheChannel()
    {
        (this.Guild.ChannelPermissionsOf(this.Guild.User1, this.Guild.General) & FakeGuild.ManageMessagesPermission)
            .ShouldBe(FakeGuild.ManageMessagesPermission);
        (this.Guild.ChannelPermissionsOf(this.Guild.User1, this.Guild.PublicThread) & FakeGuild.ManageMessagesPermission)
            .ShouldBe(FakeGuild.ManageMessagesPermission, "a thread uses its parent's overwrites");
        (this.Guild.ChannelPermissionsOf(this.Guild.User1, this.Guild.Announcements) & FakeGuild.ManageMessagesPermission)
            .ShouldBe(0UL);
        this.Guild.ChannelPermissionsOf(this.Guild.Owner, this.Guild.Announcements).ShouldBe(FakeGuild.AllPermissions);
        this.Guild.ChannelPermissionsOf(this.Guild.Admin, this.Guild.Private).ShouldBe(FakeGuild.AllPermissions);
    }

    /// <summary>XC-54 (<c>/Warn</c>) and GEN-143's first half: the channel overwrite is enough for /Warn.</summary>
    [Fact]
    public async Task Warn_WithManageMessagesOnlyInTheChannel_IsAllowed()
    {
        var reply = await this.WarnAsync(this.Guild.General);

        reply.Embed?["author"]?["name"]?.GetValue<string>().ShouldBe("Warn", reply.Message.ToJsonString());
        await using var dbContext = database.CreateDbContext();
        (await dbContext.Sins.SingleAsync(TestContext.Current.CancellationToken)).SinType.ShouldBe(SinType.Warn);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     XC-56 and GEN-143's second half: outside #general Discord hides /Warn and /Purge from <c>user1</c>, because
    ///     <c>default_member_permissions</c> is resolved per channel. The bot does not check the member's side of
    ///     <c>RequirePermissions</c> for slash commands, so an interaction that got through anyway would run. This pins
    ///     both halves: the registration that does the refusing, and that the bot does not refuse again.
    /// </summary>
    [Theory]
    [InlineData("warn")]
    [InlineData("purge")]
    public async Task OutsideTheChannel_DiscordHidesTheCommand_AndTheBotDoesNotCheckAgain(string command)
    {
        this._bot.Rest.RegisteredCommands[command]["default_member_permissions"]!.ToString()
            .ShouldBe(FakeGuild.ManageMessagesPermission.ToString(CultureInfo.InvariantCulture));

        var reply = command == "warn"
            ? await this.WarnAsync(this.Guild.Announcements)
            : await this.PurgeAllAsync(this.Guild.Announcements, this.Guild.User1);

        reply.Description.ShouldNotBe(PermissionError, reply.Message.ToJsonString());
    }

    /// <summary>XC-57: a thread inherits its parent's overwrites, so /Warn works in a thread under #general.</summary>
    [Fact]
    public async Task Warn_InAThreadUnderTheChannel_IsAllowed()
    {
        var reply = await this.WarnAsync(this.Guild.PublicThread);

        reply.Embed?["author"]?["name"]?.GetValue<string>().ShouldBe("Warn", reply.Message.ToJsonString());
    }

    /// <summary>XC-54 (<c>/Purge All</c>) and GEN-143's first half.</summary>
    [Fact]
    public async Task PurgeAll_WithManageMessagesOnlyInTheChannel_Runs()
    {
        var reply = await this.PurgeAllAsync(this.Guild.General, this.Guild.User1);

        reply.Description.ShouldNotBe(PermissionError, reply.Message.ToJsonString());
        reply.Description.ShouldNotBe(BotPermissionError);
    }

    /// <summary>XC-54 (<c>/Lock</c> and <c>/Unlock</c>): both run from #general on the channel-level overwrite.</summary>
    [Fact]
    public async Task LockAndUnlock_WithManageMessagesOnlyInTheChannel_Work()
    {
        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "lock", this.Guild.User1,
                SlashOption.Enum("duration_type", DurationType.Minutes), SlashOption.Integer("duration_amount", 2)))
            .Description.ShouldBe($"{this.Guild.General.Mention} has been locked for 2 Minutes");

        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "unlock", this.Guild.User1))
            .Description.ShouldBe($"{this.Guild.General.Mention} has been unlocked");
    }

    /// <summary>
    ///     XC-55: the <c>RequireUserGuildPermissions</c> commands read guild-level permissions, so the channel
    ///     overwrite in #general does not let <c>user1</c> run them there.
    /// </summary>
    [Theory]
    [InlineData("mute")]
    [InlineData("ban")]
    [InlineData("pardon")]
    [InlineData("user_info")]
    [InlineData("award")]
    [InlineData("publish")]
    public async Task GuildLevelCommands_IgnoreTheChannelOverwrite(string command)
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        var user = SlashOption.User("user", this.Guild.Moderator);
        SlashOption[] options = command switch
        {
            "mute" => [user, SlashOption.Enum("duration_type", DurationType.Hours), SlashOption.Integer("duration_amount", 1)],
            "pardon" => [SlashOption.Integer("sin_id", 1), SlashOption.String("reason", "x")],
            "award" => [user, SlashOption.Integer("xp", 5)],
            "publish" => [SlashOption.Subcommand("ban", SlashOption.Integer("sin_id", 1))],
            _ => [user]
        };

        var reply = await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, command, this.Guild.User1, options);

        reply.Description.ShouldBe(PermissionError);
        reply.IsEphemeral.ShouldBeTrue();
        await using var dbContext = database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>XC-58: the owner holds no role, yet Discord gives the owner every permission in every channel.</summary>
    [Fact]
    public async Task Owner_WithoutRoles_RunsTheChannelCheckedCommands()
    {
        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.Announcements, "warn", this.Guild.Owner,
                SlashOption.User("user", this.Guild.User1), SlashOption.String("reason", "x")))
            .Embed?["author"]?["name"]?.GetValue<string>().ShouldBe("Warn");
        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.ModLog, "lock", this.Guild.Owner,
                SlashOption.Enum("duration_type", DurationType.Minutes), SlashOption.Integer("duration_amount", 2)))
            .Description.ShouldBe($"{this.Guild.ModLog.Mention} has been locked for 2 Minutes");
        var purge = await this.PurgeAllAsync(this.Guild.Announcements, this.Guild.Owner);
        purge.Description.ShouldNotBe(PermissionError);
        purge.Description.ShouldNotBe(BotPermissionError);
    }

    /// <summary>XC-59: with both a missing permission and the module off, the reply lists both, one per line.</summary>
    [Fact]
    public async Task MissingPermissionAndModuleOff_AreBothShown()
    {
        await this._bot.SetModuleAsync(Module.Leveling, false);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("rewards", this.Guild.User1, SlashOption.Subcommand("view"));

        reply.Description!.Split('\n').ShouldBe([PermissionError, ModuleDisabledError]);
        reply.IsEphemeral.ShouldBeTrue();
    }

    /// <summary>
    ///     XC-60, the channel-level half: the bot has Manage Messages in the guild, but an overwrite on its role takes it
    ///     away in #news. <c>RequirePermissions</c> reads <c>app_permissions</c> for the channel, so /Purge is refused
    ///     there and still runs in #general.
    /// </summary>
    [Fact]
    public async Task BotPermissionDeniedInTheChannel_IsRefusedThere_ButNotElsewhere()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database, guild =>
        {
            guild.BotPermissions = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission
                                   | FakeGuild.EmbedLinksPermission | FakeGuild.ManageMessagesPermission;
            guild.SetOverwrite(guild.Announcements.Id, guild.BotRoleId, 0, 0, FakeGuild.ManageMessagesPermission);
        });

        (await this.PurgeAllAsync(this.Guild.Announcements, this.Guild.Admin)).Description.ShouldBe(BotPermissionError);
        (await this.PurgeAllAsync(this.Guild.General, this.Guild.Admin)).Description.ShouldNotBe(BotPermissionError);
    }

    /// <summary>
    ///     XC-61: <c>/Unlock</c> should need Manage Roles as <c>/Lock</c> does. Today its gate passes without it, Discord
    ///     refuses the overwrite, and the member gets the missing-permissions help instead of the gate's refusal.
    /// </summary>
    [Fact(Skip = "Fails: /Unlock declares only Manage Channels for the bot, so the gate passes without Manage Roles. KI-102")]
    public async Task Unlock_WithoutManageRoles_IsRefusedByTheGate()
    {
        (await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "lock", this.Guild.Moderator,
                SlashOption.Enum("duration_type", DurationType.Minutes), SlashOption.Integer("duration_amount", 5)))
            .Description.ShouldBe($"{this.Guild.General.Mention} has been locked for 5 Minutes");
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission
            | FakeGuild.ManageChannelsPermission | FakeGuild.ManageMessagesPermission);
        await this._bot.ConfigureModerationAsync();

        var reply = await this._bot.RunSlashCommandAndWaitAsync(this.Guild.General, "unlock", this.Guild.Moderator);

        reply.Description.ShouldBe(BotPermissionError);
    }

    /// <summary>XC-62: <c>/Commands Stats</c> and <c>/Commands Leaderboard</c> are open to any member, unlike Learn.</summary>
    [Theory]
    [InlineData("stats")]
    [InlineData("leaderboard")]
    public async Task CommandsStatsAndLeaderboard_AreOpenToAnyMember(string subcommand)
    {
        await this._bot.SetModuleAsync(Module.Commands, true);

        var reply = await this._bot.RunSlashCommandAndWaitAsync(this.Guild.Announcements, "commands", this.Guild.User1,
            subcommand == "stats"
                ? SlashOption.Subcommand("stats", SlashOption.String("name", "hello"))
                : SlashOption.Subcommand("leaderboard"));

        reply.Description.ShouldNotBe(PermissionError, reply.Message.ToJsonString());
        reply.Description.ShouldNotBe(ModuleDisabledError);
    }

    /// <summary>MOD-64: a role holding only Manage Messages is enough for /Warn anywhere.</summary>
    [Fact]
    public async Task Warn_ByAMemberWithOnlyManageMessages_IsAllowed()
    {
        var roleId = this.Guild.AddRole("Helper", FakeGuild.ManageMessagesPermission);
        this.Guild.SetRoles(this.Guild.User1, roleId);

        var reply = await this.WarnAsync(this.Guild.Announcements);

        reply.Embed?["author"]?["name"]?.GetValue<string>().ShouldBe("Warn", reply.Message.ToJsonString());
        await using var dbContext = database.CreateDbContext();
        (await dbContext.Sins.SingleAsync(TestContext.Current.CancellationToken)).UserId
            .ShouldBe(new UserId(this.Guild.Moderator.Id));
    }

    /// <summary>
    ///     SPAM-14: the spam filter exempts members with Manage Channels in the guild. An allow overwrite on one channel
    ///     is not enough, so <c>user1</c> is still muted for spamming there.
    /// </summary>
    [Fact]
    public async Task Spam_ByAMemberWithManageChannelsOnlyInTheChannel_IsStillMuted()
    {
        await this._bot.SetModuleAsync(Module.AntiSpam, true);
        var muteRoleId = this._bot.Rest.CreateRole("Muted");
        (await this._bot.Services.GetRequiredService<SettingsModule>().SetMuteRole(new GuildId(this.Guild.Id),
            new ModeratorId(this.Guild.Owner.Id), new RoleId(muteRoleId), cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();
        this.Guild.SetOverwrite(this.Guild.General.Id, this.Guild.User1.Id, MemberOverwrite,
            FakeGuild.ManageMessagesPermission | FakeGuild.ManageChannelsPermission, 0);
        this._bot.Gateway.Dispatch("CHANNEL_UPDATE", this.Guild.ChannelJson(this.Guild.General));
        await Task.Delay(TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);
        (this.Guild.ChannelPermissionsOf(this.Guild.User1, this.Guild.General) & FakeGuild.ManageChannelsPermission)
            .ShouldNotBe(0UL);

        for (var i = 0; i < 6; i++)
        {
            this._bot.SendMessage(this.Guild.User1, this.Guild.General, "buy cheap gold now");
            await Task.Delay(TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);
        }

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
                                                            && request.Path == $"guilds/{this.Guild.Id}/members/{this.Guild.User1.Id}/roles/{muteRoleId}");
        await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = database.CreateDbContext();
            return await dbContext.Sins.CountAsync(sin => sin.SinType == SinType.Mute, TestContext.Current.CancellationToken);
        }, count => count > 0);
    }

    private Task<FakeReply> WarnAsync(FakeChannel channel)
        => this._bot.RunSlashCommandAndWaitAsync(channel, "warn", this.Guild.User1,
            SlashOption.User("user", this.Guild.Moderator), SlashOption.String("reason", "x"));

    private Task<FakeReply> PurgeAllAsync(FakeChannel channel, FakeUser invoker)
        => this._bot.RunSlashCommandAndWaitAsync(channel, "purge", invoker,
            SlashOption.Subcommand("all", SlashOption.Integer("count", 1)));
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Enums;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     <c>/GeneralSettings</c> View, ModLogChannel and UserCommands, section B of
///     <c>docs/testing/01-general-settings.md</c>. GEN-22 (two guilds) is covered in A-35, GEN-36 (Embed Links)
///     stays manual, and GEN-56 to GEN-59 (how <c>/Level</c> and <c>/Leaderboard</c> honour the channel) in A-26.
/// </summary>
[Collection("Test collection")]
public sealed class GeneralSettingsCommandsTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string SelectChannelError = "Select a channel when using the Select Channel option.";
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── View ──────────────────────────────────────────────────────────────────

    /// <summary>GEN-20.</summary>
    [Fact]
    public async Task View_OnAFreshGuild_ShowsNoneForBoth_Ephemerally()
    {
        var reply = await this.ViewAsync();

        reply.IsEphemeral.ShouldBeTrue();
        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("General Settings");
        reply.Description.ShouldBe("**Moderation Log:** None\n**User Command Channel:** None");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>GEN-21.</summary>
    [Fact]
    public async Task View_ShowsConfiguredChannelsAsMentions()
    {
        await this.Settings.SetLogChannelSetting(GuildLogType.Moderation, this.GuildId, this.OwnerId, new ChannelId(this.Guild.ModLog.Id), TestContext.Current.CancellationToken);
        await this.Settings.SetUserCommandChannelSetting(this.GuildId, this.OwnerId, new ChannelId(this.Guild.General.Id), TestContext.Current.CancellationToken);

        (await this.ViewAsync()).Description.ShouldBe(
            $"**Moderation Log:** {this.Guild.ModLog.Mention}\n**User Command Channel:** {this.Guild.General.Mention}");
    }

    /// <summary>GEN-23: a channel deleted after being set still shows as its mention, without an error.</summary>
    [Fact]
    public async Task View_WithADeletedChannel_ShowsItsMentionWithoutFailing()
    {
        const ulong deletedChannelId = 1_100_000_000_000_000_299;
        await this.Settings.SetLogChannelSetting(GuildLogType.Moderation, this.GuildId, this.OwnerId, new ChannelId(deletedChannelId), TestContext.Current.CancellationToken);

        (await this.ViewAsync()).Description.ShouldBe($"**Moderation Log:** <#{deletedChannelId}>\n**User Command Channel:** None");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    // ── ModLogChannel ─────────────────────────────────────────────────────────

    /// <summary>GEN-30: the reply, the stored setting, and the change announced in the new mod log.</summary>
    [Fact]
    public async Task ModLogChannel_SelectChannel_SetsItAndAnnouncesIt()
    {
        var reply = await this.ModLogChannelAsync(ChannelOption.SelectChannel, this.Guild.ModLog);

        reply.IsEphemeral.ShouldBeFalse();
        reply.Description.ShouldBe($"Updated the moderation log to {this.Guild.ModLog.Mention}");
        (await this.ModLogSettingAsync()).ShouldBe(this.Guild.ModLog.Id);
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} updated the moderation log to {this.Guild.ModLog.Mention}.");
    }

    /// <summary>GEN-31 and GEN-39: Current Channel uses the channel the command ran in, a thread included.</summary>
    [Theory]
    [InlineData("general")]
    [InlineData("thread")]
    public async Task ModLogChannel_CurrentChannel_UsesTheChannelTheCommandRanIn(string where)
    {
        var channel = where == "general" ? this.Guild.General : this.Guild.PublicThread;

        var reply = await this._bot.RunSlashCommandAndWaitAsync(channel, "general_settings", this.Guild.Admin,
            SlashOption.Subcommand("mod_log_channel", SlashOption.Enum("option", ChannelOption.CurrentChannel)));

        reply.Description.ShouldBe($"Updated the moderation log to {channel.Mention}");
        (await this.ModLogSettingAsync()).ShouldBe(channel.Id);
    }

    /// <summary>GEN-33: once changed, the announcement goes to the new channel, not the old one.</summary>
    [Fact]
    public async Task ModLogChannel_Changing_AnnouncesInTheNewChannel()
    {
        await this.Settings.SetLogChannelSetting(GuildLogType.Moderation, this.GuildId, this.OwnerId, new ChannelId(this.Guild.ModLog.Id), TestContext.Current.CancellationToken);

        await this.ModLogChannelAsync(ChannelOption.SelectChannel, this.Guild.General);

        var announcement = await this._bot.Rest.WaitForRequestAsync(request =>
            request.Method == HttpMethod.Post && request.Path == $"channels/{this.Guild.General.Id}/messages");
        announcement.Json!["embeds"]![0]!["description"]!.GetValue<string>()
            .ShouldBe($"{this.Guild.Admin.MemberMention} updated the moderation log to {this.Guild.General.Mention}.");
        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>GEN-32, GEN-34 and GEN-38: Off clears the setting even with a channel given, and posts no log.</summary>
    [Fact]
    public async Task ModLogChannel_Off_ClearsTheSetting_IgnoresAnyChannel_AndPostsNothing()
    {
        await this.Settings.SetLogChannelSetting(GuildLogType.Moderation, this.GuildId, this.OwnerId, new ChannelId(this.Guild.ModLog.Id), TestContext.Current.CancellationToken);

        var reply = await this.ModLogChannelAsync(ChannelOption.Off, this.Guild.General);

        reply.Description.ShouldNotBeNull().ShouldStartWith($"{this.Guild.Admin.MemberMention} disabled the moderation log");
        (await this.ModLogSettingAsync()).ShouldBeNull();
        (await this._bot.Rest.ReceivesRequestWithinAsync(
            request => request.Method == HttpMethod.Post && request.Path.EndsWith("/messages", StringComparison.Ordinal),
            NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>GEN-32, area 01 code observation 2.</summary>
    [Fact(Skip = "Fails: the Off reply ends with a dangling \"to \" and an empty mention. KI-22")]
    public async Task ModLogChannel_Off_ReplyReadsCleanly()
        => (await this.ModLogChannelAsync(ChannelOption.Off)).Description
            .ShouldBe($"{this.Guild.Admin.MemberMention} disabled the moderation log.");

    /// <summary>GEN-35: the bot must be able to send in the channel. Administrator would ignore the overwrite.</summary>
    [Fact]
    public async Task ModLogChannel_WhereTheBotCannotSend_IsRefused_AndChangesNothing()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);

        var reply = await this.ModLogChannelAsync(ChannelOption.SelectChannel, this.Guild.Private);

        reply.Description.ShouldBe($"{this.Guild.Bot.MemberMention} doesn't have permission to send messages in that channel.");
        (await this.ModLogSettingAsync()).ShouldBeNull();
    }

    /// <summary>GEN-37 and GEN-54: Select Channel without a channel is rejected and changes nothing.</summary>
    [Theory]
    [InlineData("mod_log_channel")]
    [InlineData("user_commands")]
    public async Task SelectChannelWithoutAChannel_IsRejected(string subcommand)
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("general_settings", this.Guild.Admin,
            SlashOption.Subcommand(subcommand, SlashOption.Enum("option", ChannelOption.SelectChannel)));

        reply.Description.ShouldBe(SelectChannelError);
        (await this.ModLogSettingAsync()).ShouldBeNull();
        (await this.Settings.GetUserCommandChannel(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeNull();
    }

    /// <summary>GEN-37: errors sent after a public defer can't be ephemeral, so everyone in the channel sees them.</summary>
    [Fact(Skip = "Fails: the command defers publicly, so its ephemeral error edits the public response. KI-21")]
    public async Task SelectChannelWithoutAChannel_ErrorIsEphemeral()
        => (await this._bot.RunSlashCommandAndWaitAsync("general_settings", this.Guild.Admin,
                SlashOption.Subcommand("mod_log_channel", SlashOption.Enum("option", ChannelOption.SelectChannel))))
            .IsEphemeral.ShouldBeTrue();

    /// <summary>GEN-40 and GEN-60.</summary>
    [Theory]
    [InlineData("mod_log_channel")]
    [InlineData("user_commands")]
    public async Task ChangingASetting_WithoutManageGuild_IsDenied(string subcommand)
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("general_settings", this.Guild.Moderator,
            SlashOption.Subcommand(subcommand, SlashOption.Enum("option", ChannelOption.SelectChannel),
                SlashOption.Channel("channel", this.Guild.General)));

        reply.Description.ShouldBe("You do not have the required permissions to use this command.");
        (await this.ModLogSettingAsync()).ShouldBeNull();
        (await this.Settings.GetUserCommandChannel(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeNull();
    }

    // ── UserCommands ──────────────────────────────────────────────────────────

    /// <summary>GEN-50.</summary>
    [Fact]
    public async Task UserCommands_SelectChannel_SetsItAndLogsIt()
    {
        await this._bot.ConfigureModerationAsync();

        var reply = await this.UserCommandsAsync(ChannelOption.SelectChannel, this.Guild.General);

        reply.Description.ShouldBe($"Updated the User Command Channel to {this.Guild.General.Mention}");
        (await this.Settings.GetUserCommandChannel(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBe(new ChannelId(this.Guild.General.Id));
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} updated the User Command Channel to {this.Guild.General.Mention}.");
    }

    /// <summary>GEN-51.</summary>
    [Fact]
    public async Task UserCommands_CurrentChannel_UsesTheChannelTheCommandRanIn()
    {
        await this._bot.RunSlashCommandAndWaitAsync(this.Guild.ModLog, "general_settings", this.Guild.Admin,
            SlashOption.Subcommand("user_commands", SlashOption.Enum("option", ChannelOption.CurrentChannel)));

        (await this.Settings.GetUserCommandChannel(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBe(new ChannelId(this.Guild.ModLog.Id));
    }

    /// <summary>GEN-52.</summary>
    [Fact]
    public async Task UserCommands_Off_ClearsItAndLogsIt()
    {
        await this._bot.ConfigureModerationAsync();
        await this.Settings.SetUserCommandChannelSetting(this.GuildId, this.OwnerId, new ChannelId(this.Guild.General.Id), TestContext.Current.CancellationToken);

        (await this.UserCommandsAsync(ChannelOption.Off)).Description.ShouldBe("Disabled the User Command Channel.");

        (await this.Settings.GetUserCommandChannel(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeNull();
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} disabled the User Command Channel.");
    }

    /// <summary>
    ///     GEN-53, area 01 code observation 3: unlike ModLogChannel, there is no check that the bot can send in the
    ///     channel. That may be fine, since replies are interaction responses. This pins the current behaviour.
    /// </summary>
    [Fact]
    public async Task UserCommands_AcceptsAChannelTheBotCannotSendIn()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);

        (await this.UserCommandsAsync(ChannelOption.SelectChannel, this.Guild.Private)).Description
            .ShouldBe($"Updated the User Command Channel to {this.Guild.Private.Mention}");
        (await this.Settings.GetUserCommandChannel(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBe(new ChannelId(this.Guild.Private.Id));
    }

    /// <summary>GEN-55.</summary>
    [Fact]
    public async Task UserCommands_WithNoModLog_WorksQuietly()
    {
        (await this.UserCommandsAsync(ChannelOption.SelectChannel, this.Guild.General)).Description
            .ShouldBe($"Updated the User Command Channel to {this.Guild.General.Mention}");

        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    private ModeratorId OwnerId => new(this.Guild.Owner.Id);

    private Task<FakeReply> ViewAsync()
        => this._bot.RunSlashCommandAndWaitAsync("general_settings", this.Guild.Admin, SlashOption.Subcommand("view"));

    private Task<FakeReply> ModLogChannelAsync(ChannelOption option, FakeChannel? channel = null)
        => this.SettingAsync("mod_log_channel", option, channel);

    private Task<FakeReply> UserCommandsAsync(ChannelOption option, FakeChannel? channel = null)
        => this.SettingAsync("user_commands", option, channel);

    private Task<FakeReply> SettingAsync(string subcommand, ChannelOption option, FakeChannel? channel)
        => this._bot.RunSlashCommandAndWaitAsync("general_settings", this.Guild.Admin,
            SlashOption.Subcommand(subcommand,
                channel is null
                    ? [SlashOption.Enum("option", option)]
                    : [SlashOption.Enum("option", option), SlashOption.Channel("channel", channel)]));

    private async Task<ulong?> ModLogSettingAsync()
        => (await this.Settings.GetLogChannelSetting(GuildLogType.Moderation, this.GuildId)).ShouldSucceed()?.Value;
}

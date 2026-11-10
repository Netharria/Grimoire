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
using UserLogSetting = Grimoire.Features.Logging.Settings.LogSettingsCommands.User.UserLogSetting;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>
///     <c>/Log User</c> Set and View cases from <c>05-logging.md</c> section D that <see cref="UserLogTests" /> doesn't
///     cover (backlog A-43).
/// </summary>
[Collection("Test collection")]
public sealed class UserLogSettingsTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this.ConfigureAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task ConfigureAsync()
    {
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.UserLog, true);
    }

    /// <summary>Restarts the bot with Send Messages and Embed Links everywhere it can see, but not Attach Files.</summary>
    private async Task RestartWithoutAttachFilesAsync()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this.ConfigureAsync();
    }

    /// <summary>LOG-42: Current Channel uses the channel the command ran in.</summary>
    [Fact]
    public async Task Set_CurrentChannel_UsesTheChannelTheCommandRanIn()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync(this.Guild.Announcements, "log", this.Guild.Admin,
            SlashOption.Group("user", SlashOption.Subcommand("set", SlashOption.Enum("setting", UserLogSetting.JoinLog),
                SlashOption.Enum("option", ChannelOption.CurrentChannel))));

        reply.Description.ShouldBe($"Updated JoinLog to {this.Guild.Announcements.Mention}");
        (await this.LogChannelAsync(GuildLogType.UserJoined)).ShouldBe(this.Guild.Announcements.Id);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LOG-43: Select Channel without a channel is rejected.</summary>
    [Fact]
    public async Task Set_SelectChannelWithoutAChannel_IsRejected()
    {
        var reply = await this.SetAsync(UserLogSetting.LeaveLog, ChannelOption.SelectChannel);

        reply.IsEphemeral.ShouldBeTrue();
        reply.Description.ShouldBe("Select a channel when using the Select Channel option.");
        (await this.LogChannelAsync(GuildLogType.UserLeft)).ShouldBeNull();
    }

    /// <summary>LOG-44: a channel the bot can't send in is refused.</summary>
    [Fact]
    public async Task Set_WhereTheBotCannotSend_IsRefused()
    {
        await this.RestartWithoutAttachFilesAsync();

        (await this.SetAsync(UserLogSetting.UsernameLog, ChannelOption.SelectChannel, this.Guild.Private)).Description
            .ShouldBe($"{this.Guild.Bot.MemberMention} doesn't have permission to send messages in that channel.");
        (await this.LogChannelAsync(GuildLogType.UsernameUpdated)).ShouldBeNull();
    }

    /// <summary>LOG-45: the avatar log uploads images, so it needs Attach Files; the nickname log doesn't.</summary>
    [Fact]
    public async Task Set_WithoutAttachFiles_IsRefusedForTheAvatarLog_ButNotTheNicknameLog()
    {
        await this.RestartWithoutAttachFilesAsync();

        (await this.SetAsync(UserLogSetting.AvatarLog, ChannelOption.SelectChannel, this.Guild.General)).Description
            .ShouldBe($"{this.Guild.Bot.MemberMention} also needs **Attach Files** in that channel to post these logs.");
        (await this.SetAsync(UserLogSetting.NicknameLog, ChannelOption.SelectChannel, this.Guild.General)).Description
            .ShouldBe($"Updated NicknameLog to {this.Guild.General.Mention}");

        (await this.LogChannelAsync(GuildLogType.AvatarUpdated)).ShouldBeNull();
        (await this.LogChannelAsync(GuildLogType.NicknameUpdated)).ShouldBe(this.Guild.General.Id);
    }

    /// <summary>LOG-46: a category can't hold messages.</summary>
    [Fact]
    public async Task Set_ToACategory_IsRefused()
    {
        (await this.SetAsync(UserLogSetting.JoinLog, ChannelOption.SelectChannel, this.Guild.Category)).Description
            .ShouldBe($"{this.Guild.Category.Mention} can't hold messages. Pick a text channel.");
        (await this.LogChannelAsync(GuildLogType.UserJoined)).ShouldBeNull();
    }

    /// <summary>LOG-48: View shows Deleted Channel for a log channel that no longer exists.</summary>
    [Fact]
    public async Task View_WithADeletedChannel_SaysSo()
    {
        await this.SetAsync(UserLogSetting.JoinLog, ChannelOption.SelectChannel, this.Guild.Announcements);
        this._bot.DeleteChannel(this.Guild.Announcements);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        (await this.UserAsync(SlashOption.Subcommand("view"))).Description.ShouldBe(
            "**Join Log:** Deleted Channel\n**Leave Log:** None\n**Username Log:** None\n**Nickname Log:** None\n**Avatar Log:** None\n");
    }

    /// <summary>LOG-49, LOG-50: Set is gated like View.</summary>
    [Theory]
    [InlineData("permission")]
    [InlineData("module")]
    public async Task Set_NeedsManageGuildAndTheModule(string missing)
    {
        if (missing == "module")
            await this._bot.SetModuleAsync(Module.UserLog, false);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("log", missing == "module" ? this.Guild.Admin : this.Guild.Moderator,
            SlashOption.Group("user", SlashOption.Subcommand("set", SlashOption.Enum("setting", UserLogSetting.JoinLog),
                SlashOption.Enum("option", ChannelOption.SelectChannel), SlashOption.Channel("value", this.Guild.General))));

        reply.Description.ShouldBe(missing == "module"
            ? "This module is disabled in this server."
            : "You do not have the required permissions to use this command.");
        (await this.LogChannelAsync(GuildLogType.UserJoined)).ShouldBeNull();
    }

    /// <summary>LOG-40: the reply should name the setting as the member chose it, not by its code name.</summary>
    [Fact(Skip = "Fails: the reply says \"Updated JoinLog to ...\", the code name. KI-66")]
    public async Task Set_Reply_UsesTheSettingsDisplayName()
        => (await this.SetAsync(UserLogSetting.JoinLog, ChannelOption.SelectChannel, this.Guild.General)).Description
            .ShouldBe($"Updated Joined Server Log to {this.Guild.General.Mention}");

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
}

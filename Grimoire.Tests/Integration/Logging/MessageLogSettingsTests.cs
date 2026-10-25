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
using LogSetting = Grimoire.Features.Logging.Settings.LogSettingsCommands.Message.MessageLogSetting;
using OverrideSetting = Grimoire.Features.Logging.Settings.LogSettingsCommands.Message.MessageLogOverrideSetting;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>
///     <c>/Log Message</c> Set, View, Override and ViewOverrides (backlog A-21). Overrides are checked by their effect:
///     whether the message log saves a message sent in that channel.
/// </summary>
[Collection("Test collection")]
public sealed class MessageLogSettingsTests(GrimoireCoreFactory database) : IAsyncLifetime
{
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
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureMessageLogAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── View and Set ──────────────────────────────────────────────────────────

    [Fact]
    public async Task View_OnAFreshGuild_ShowsNoLogChannels_Ephemerally()
    {
        var reply = await this.MessageAsync(SlashOption.Subcommand("view"));

        reply.IsEphemeral.ShouldBeTrue();
        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Current Logging System Settings");
        reply.Description.ShouldBe("**Module Enabled:** True\n**Delete Log:** None\n**Bulk Delete Log:** None\n**Edit Log:** None\n");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>Each log can be pointed at a channel, which is stored, logged and shown by View.</summary>
    [Theory]
    [InlineData(LogSetting.DeleteLog, GuildLogType.MessageDeleted, "Delete Log")]
    [InlineData(LogSetting.BulkDeleteLog, GuildLogType.BulkMessageDeleted, "Bulk Delete Log")]
    [InlineData(LogSetting.EditLog, GuildLogType.MessageEdited, "Edit Log")]
    public async Task Set_StoresTheChannel_LogsIt_AndViewShowsIt(LogSetting setting, GuildLogType logType, string label)
    {
        (await this.SetAsync(setting, ChannelOption.SelectChannel, this.Guild.General)).Description
            .ShouldBe($"Updated {setting} to {this.Guild.General.Mention}");

        (await this.LogChannelAsync(logType)).ShouldBe(this.Guild.General.Id);
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} updated {setting} to {this.Guild.General.Mention}.");
        (await this.MessageAsync(SlashOption.Subcommand("view"))).Description.ShouldNotBeNull()
            .ShouldContain($"**{label}:** {this.Guild.General.Mention}");
    }

    [Fact]
    public async Task Set_Off_ClearsTheChannel()
    {
        await this.SetAsync(LogSetting.EditLog, ChannelOption.SelectChannel, this.Guild.General);

        (await this.SetAsync(LogSetting.EditLog, ChannelOption.Off, this.Guild.General)).Description.ShouldBe("Disabled EditLog");

        (await this.LogChannelAsync(GuildLogType.MessageEdited)).ShouldBeNull();
    }

    [Fact]
    public async Task Set_CurrentChannel_UsesTheChannelTheCommandRanIn()
    {
        await this._bot.RunSlashCommandAndWaitAsync(this.Guild.ModLog, "log", this.Guild.Admin,
            SlashOption.Group("message", SlashOption.Subcommand("set", SlashOption.Enum("setting", LogSetting.DeleteLog),
                SlashOption.Enum("option", ChannelOption.CurrentChannel))));

        (await this.LogChannelAsync(GuildLogType.MessageDeleted)).ShouldBe(this.Guild.ModLog.Id);
    }

    [Fact]
    public async Task Set_WhereTheBotCannotSend_IsRefused()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this._bot.ConfigureMessageLogAsync();

        (await this.SetAsync(LogSetting.DeleteLog, ChannelOption.SelectChannel, this.Guild.Private)).Description
            .ShouldBe($"{this.Guild.Bot.MemberMention} don't have permission to send messages in that channel.");
        (await this.LogChannelAsync(GuildLogType.MessageDeleted)).ShouldBeNull();
    }

    [Fact]
    public async Task Set_SelectChannelWithoutAChannel_IsRejected()
        => (await this.SetAsync(LogSetting.DeleteLog, ChannelOption.SelectChannel)).Description
            .ShouldBe("Select a channel when using the Select Channel option.");

    [Fact]
    public async Task View_WithADeletedChannel_SaysSo()
    {
        await this.SetAsync(LogSetting.DeleteLog, ChannelOption.SelectChannel, this.Guild.Forum);
        this._bot.DeleteChannel(this.Guild.Forum);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        (await this.MessageAsync(SlashOption.Subcommand("view"))).Description.ShouldNotBeNull()
            .ShouldContain("**Delete Log:** Deleted Channel");
    }

    // ── Overrides ─────────────────────────────────────────────────────────────

    /// <summary>Never stops a channel being logged, ViewOverrides lists it, and Inherit puts logging back.</summary>
    [Fact]
    public async Task NeverOverride_StopsLogging_AndInheritRestoresIt()
    {
        (await this.OverrideAsync(OverrideSetting.Never, this.Guild.General)).Description
            .ShouldBe($"Will now never log messages from {this.Guild.General.Mention} and its sub channels/threads.");
        (await this.MessageSavedWithinAsync(this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hidden"), NothingHappensWindow))
            .ShouldBeFalse();
        (await this.MessageAsync(SlashOption.Subcommand("view_overrides"))).Description.ShouldNotBeNull()
            .ShouldContain($"{this.Guild.General.Mention} - Never Log");

        (await this.OverrideAsync(OverrideSetting.Inherit, this.Guild.General)).Description
            .ShouldBe("Override was successfully removed from the channel.");
        (await this.MessageSavedWithinAsync(this._bot.SendMessage(this.Guild.User1, this.Guild.General, "shown"), TimeSpan.FromSeconds(10)))
            .ShouldBeTrue();
    }

    /// <summary>A thread's Always override wins over its parent's Never.</summary>
    [Fact]
    public async Task AlwaysOverrideOnAThread_WinsOverItsParent()
    {
        await this.OverrideAsync(OverrideSetting.Never, this.Guild.Forum);
        (await this.MessageSavedWithinAsync(this._bot.SendMessage(this.Guild.User1, this.Guild.ForumPost, "hidden"), NothingHappensWindow))
            .ShouldBeFalse("the parent's Never applies to its threads");

        await this.OverrideAsync(OverrideSetting.Always, this.Guild.ForumPost);

        (await this.MessageSavedWithinAsync(this._bot.SendMessage(this.Guild.User1, this.Guild.ForumPost, "shown"), TimeSpan.FromSeconds(10)))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task Override_IsLoggedToTheModLog()
    {
        await this.OverrideAsync(OverrideSetting.Never, this.Guild.General);

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"Will now never log messages from {this.Guild.General.Mention} and its sub channels/threads.");
    }

    // ── Gates ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("permission")]
    [InlineData("module")]
    public async Task LogMessage_NeedsManageGuildAndTheModule(string missing)
    {
        if (missing == "module")
            await this._bot.SetModuleAsync(Module.MessageLog, false);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("log", missing == "module" ? this.Guild.Admin : this.Guild.Moderator,
            SlashOption.Group("message", SlashOption.Subcommand("view")));

        reply.Description.ShouldBe(missing == "module"
            ? "This module is disabled in this server."
            : "You do not have the required permissions to use this command.");
    }

    private Task<FakeReply> MessageAsync(SlashOption subcommand)
        => this._bot.RunSlashCommandAndWaitAsync("log", this.Guild.Admin, SlashOption.Group("message", subcommand));

    private Task<FakeReply> SetAsync(LogSetting setting, ChannelOption option, FakeChannel? channel = null)
        => this.MessageAsync(SlashOption.Subcommand("set",
        [
            SlashOption.Enum("setting", setting), SlashOption.Enum("option", option),
            .. channel is null ? Array.Empty<SlashOption>() : [SlashOption.Channel("value", channel)]
        ]));

    private Task<FakeReply> OverrideAsync(OverrideSetting setting, FakeChannel channel)
        => this.MessageAsync(SlashOption.Subcommand("override", SlashOption.Enum("option", setting), SlashOption.Channel("channel", channel)));

    private async Task<ulong?> LogChannelAsync(GuildLogType logType)
        => (await this.Settings.GetLogChannelSetting(logType, this.GuildId)).ShouldSucceed()?.Value;

    private Task<bool> MessageSavedWithinAsync(ulong messageId, TimeSpan window)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId));
        }, saved => saved, window);
}

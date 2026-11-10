// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Enums;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;
using LogSetting = Grimoire.Features.Logging.Settings.LogSettingsCommands.Message.MessageLogSetting;
using OverrideSetting = Grimoire.Features.Logging.Settings.LogSettingsCommands.Message.MessageLogOverrideSetting;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>
///     <c>/Log Message</c> cases from <c>05-logging.md</c> sections A to C that <see cref="MessageLogSettingsTests" />
///     doesn't cover, plus XC-73 (backlog A-43).
/// </summary>
[Collection("Test collection")]
public sealed class MessageLogSettingsGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database, secondGuild: true);
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureMessageLogAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Set ───────────────────────────────────────────────────────────────────

    /// <summary>LOG-05: setting the channel it already has counts as saved, and the mod log gets the line again.</summary>
    [Fact]
    public async Task Set_ToTheSameChannelAgain_RepliesAndLogsAgain()
    {
        var logLine = $"{this.Guild.Admin.MemberMention} updated DeleteLog to {this.Guild.General.Mention}.";
        await this.SetAsync(LogSetting.DeleteLog, ChannelOption.SelectChannel, this.Guild.General);
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>() == logLine);

        (await this.SetAsync(LogSetting.DeleteLog, ChannelOption.SelectChannel, this.Guild.General)).Description
            .ShouldBe($"Updated DeleteLog to {this.Guild.General.Mention}");

        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.ModLogPostsWith(logLine)), count => count == 2))
            .ShouldBe(2);
        (await this.LogChannelAsync(GuildLogType.MessageDeleted)).ShouldBe(this.Guild.General.Id);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LOG-07: Off ignores a channel given with it.</summary>
    [Fact]
    public async Task Set_OffWithAChannel_IgnoresTheChannel()
    {
        await this.SetAsync(LogSetting.DeleteLog, ChannelOption.SelectChannel, this.Guild.ModLog);

        (await this.SetAsync(LogSetting.DeleteLog, ChannelOption.Off, this.Guild.General)).Description
            .ShouldBe("Disabled DeleteLog.");

        (await this.LogChannelAsync(GuildLogType.MessageDeleted)).ShouldBeNull();
    }

    /// <summary>LOG-08 (category): a category can't hold messages, so it can't be a log channel.</summary>
    [Fact]
    public async Task Set_ToACategory_IsRefused()
    {
        (await this.SetAsync(LogSetting.DeleteLog, ChannelOption.SelectChannel, this.Guild.Category)).Description
            .ShouldBe($"{this.Guild.Category.Mention} can't hold messages. Pick a text channel.");
        (await this.LogChannelAsync(GuildLogType.MessageDeleted)).ShouldBeNull();
    }

    /// <summary>LOG-13: log channels are per guild.</summary>
    [Fact]
    public async Task Set_InOneGuild_LeavesTheOtherGuildsViewAtNone()
    {
        var second = this._bot.SecondGuild!;
        await this._bot.SetModuleAsync(Module.MessageLog, true, second);
        await this.SetAsync(LogSetting.DeleteLog, ChannelOption.SelectChannel, this.Guild.General);
        (await this.MessageAsync(SlashOption.Subcommand("view"))).Description.ShouldNotBeNull()
            .ShouldContain($"**Delete Log:** {this.Guild.General.Mention}");

        var reply = await this._bot.RunSlashCommandAndWaitAsync(second.General, "log", second.Admin,
            SlashOption.Group("message", SlashOption.Subcommand("view")));

        reply.Description.ShouldBe("**Delete Log:** None\n**Bulk Delete Log:** None\n**Edit Log:** None\n");
    }

    /// <summary>LOG-01: the reply should name the setting as the member chose it, not by its code name.</summary>
    [Fact(Skip = "Fails: the reply says \"Updated DeleteLog to ...\", the code name. KI-66")]
    public async Task Set_Reply_UsesTheSettingsDisplayName()
        => (await this.SetAsync(LogSetting.DeleteLog, ChannelOption.SelectChannel, this.Guild.General)).Description
            .ShouldBe($"Updated Delete Message Log to {this.Guild.General.Mention}");

    // ── Override ──────────────────────────────────────────────────────────────

    /// <summary>LOG-25: with no channel option, the override applies to the channel the command ran in.</summary>
    [Fact]
    public async Task Override_WithoutAChannel_AppliesToTheCurrentChannel()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync(this.Guild.Announcements, "log", this.Guild.Admin,
            SlashOption.Group("message", SlashOption.Subcommand("override", SlashOption.Enum("option", OverrideSetting.Never))));

        reply.Description.ShouldBe(
            $"Will now never log messages from {this.Guild.Announcements.Mention} and its sub channels/threads.");
        var row = (await this.OverrideRowsAsync()).ShouldHaveSingleItem();
        row.ChannelId.ShouldBe(new ChannelId(this.Guild.Announcements.Id));
        row.ChannelOption.ShouldBe(MessageLogOverrideOption.NeverLog);
    }

    /// <summary>LOG-27: Inherit on a channel with no override still says it was removed, and stores nothing.</summary>
    [Fact]
    public async Task Override_InheritWithNoOverride_RepliesRemoved_AndStoresNothing()
    {
        (await this.OverrideAsync(OverrideSetting.Inherit, this.Guild.General)).Description
            .ShouldBe("Override was successfully removed from the channel.");

        (await this.OverrideRowsAsync()).ShouldBeEmpty();
    }

    /// <summary>LOG-28: an override only stops new messages being saved; edits and deletes of saved ones still log.</summary>
    [Fact]
    public async Task Override_Never_StillLogsEditsAndDeletesOfMessagesSavedBefore()
    {
        foreach (var logType in new[] { GuildLogType.MessageEdited, GuildLogType.MessageDeleted })
            (await this.Settings.SetLogChannelSetting(logType, this.GuildId, new ModeratorId(this.Guild.Owner.Id),
                new ChannelId(this.Guild.ModLog.Id), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Guild.General, "before");
        await GrimoireTestBot.EventuallyAsync(() => this.MessageSavedAsync(messageId), saved => saved);
        await this.OverrideAsync(OverrideSetting.Never, this.Guild.General);

        this._bot.EditMessage(messageId, this.Guild.User1, this.Guild.General, "after");
        await this.WaitForLogAsync($"Message edited in #{this.Guild.General.Name}");
        this._bot.DeleteMessage(messageId, this.Guild.General);
        await this.WaitForLogAsync($"Message deleted in #{this.Guild.General.Name}");

        (await this.HistoryOfAsync<MessageEditedEntry>(messageId)).ShouldHaveSingleItem();
        (await GrimoireTestBot.EventuallyAsync(() => this.HistoryOfAsync<MessageDeletedEntry>(messageId), rows => rows.Count == 1))
            .ShouldHaveSingleItem();
    }

    /// <summary>LOG-29: Override needs the module and Manage Server, like the rest of the group.</summary>
    [Theory]
    [InlineData("permission")]
    [InlineData("module")]
    public async Task Override_NeedsManageGuildAndTheModule(string missing)
    {
        if (missing == "module")
            await this._bot.SetModuleAsync(Module.MessageLog, false);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("log", missing == "module" ? this.Guild.Admin : this.Guild.Moderator,
            SlashOption.Group("message", SlashOption.Subcommand("override", SlashOption.Enum("option", OverrideSetting.Never),
                SlashOption.Channel("channel", this.Guild.General))));

        reply.Description.ShouldBe(missing == "module"
            ? "This module is disabled in this server."
            : "You do not have the required permissions to use this command.");
        (await this.OverrideRowsAsync()).ShouldBeEmpty();
    }

    // ── ViewOverrides ─────────────────────────────────────────────────────────

    /// <summary>LOG-30: Never and Always overrides are listed publicly; an Inherit one is left out.</summary>
    [Fact]
    public async Task ViewOverrides_ListsNeverAndAlways_ButNotInherit()
    {
        await this.OverrideAsync(OverrideSetting.Never, this.Guild.Announcements);
        await this.OverrideAsync(OverrideSetting.Always, this.Guild.PublicThread);
        await this.OverrideAsync(OverrideSetting.Never, this.Guild.General);
        await this.OverrideAsync(OverrideSetting.Inherit, this.Guild.General);

        var reply = await this.MessageAsync(SlashOption.Subcommand("view_overrides"));

        reply.IsEphemeral.ShouldBeFalse();
        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Channel Override Settings");
        reply.Description.ShouldNotBeNull().Split('\n').ShouldBe(
            [$"{this.Guild.Announcements.Mention} - Never Log", $"{this.Guild.PublicThread.Mention} - Always Log"],
            ignoreOrder: true);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LOG-31: with no overrides, the embed should say so rather than be blank.</summary>
    [Fact(Skip = "Fails: with no overrides the embed has a heading and an empty body. KI-71")]
    public async Task ViewOverrides_WithNoOverrides_SaysSo()
        => (await this.MessageAsync(SlashOption.Subcommand("view_overrides"))).Description
            .ShouldNotBeNullOrWhiteSpace();

    /// <summary>
    ///     LOG-32: an override on a deleted channel is left out of the list. Its row stays, which is the other half of
    ///     KI-71; this test pins that.
    /// </summary>
    [Fact]
    public async Task ViewOverrides_LeavesOutDeletedChannels_ButKeepsTheirRows()
    {
        await this.OverrideAsync(OverrideSetting.Never, this.Guild.Announcements);
        await this.OverrideAsync(OverrideSetting.Never, this.Guild.General);
        this._bot.DeleteChannel(this.Guild.Announcements);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        (await this.MessageAsync(SlashOption.Subcommand("view_overrides"))).Description
            .ShouldBe($"{this.Guild.General.Mention} - Never Log");

        (await this.OverrideRowsAsync()).Select(row => row.ChannelId)
            .ShouldContain(new ChannelId(this.Guild.Announcements.Id));
    }

    // ── Module gates (XC-73) ──────────────────────────────────────────────────

    /// <summary>
    ///     XC-73, LOG-19, LOG-50: both Log Views are still refused with their module off, unlike <c>/ModSettings View</c>
    ///     and <c>/LevelSettings View</c>. KI-65 is a decision; this pins the current behaviour.
    /// </summary>
    [Fact]
    public async Task LogViews_WithTheirModulesOff_AreBothRefused()
    {
        await this._bot.SetModuleAsync(Module.MessageLog, false);
        await this._bot.SetModuleAsync(Module.UserLog, false);

        foreach (var group in new[] { "message", "user" })
            (await this._bot.RunSlashCommandAndWaitAsync("log", this.Guild.Admin,
                    SlashOption.Group(group, SlashOption.Subcommand("view"))))
                .Description.ShouldBe("This module is disabled in this server.", group);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<FakeReply> MessageAsync(SlashOption subcommand)
        => this._bot.RunSlashCommandAndWaitAsync("log", this.Guild.Admin, SlashOption.Group("message", subcommand));

    private Task<FakeReply> SetAsync(LogSetting setting, ChannelOption option, FakeChannel? channel = null)
        => this.MessageAsync(SlashOption.Subcommand("set",
        [
            SlashOption.Enum("setting", setting), SlashOption.Enum("option", option),
            .. channel is null ? Array.Empty<SlashOption>() : [SlashOption.Channel("value", channel)]
        ]));

    private Task<FakeReply> OverrideAsync(OverrideSetting setting, FakeChannel channel)
        => this.MessageAsync(SlashOption.Subcommand("override", SlashOption.Enum("option", setting),
            SlashOption.Channel("channel", channel)));

    private async Task<ulong?> LogChannelAsync(GuildLogType logType)
        => (await this.Settings.GetLogChannelSetting(logType, this.GuildId)).ShouldSucceed()?.Value;

    private int ModLogPostsWith(string description)
        => this._bot.Rest.Requests.Count(request => this._bot.IsModLogPost(request)
                                                    && request.Json?["embeds"]?[0]?["description"]?.ToString() == description);

    private async Task<List<MessageLogChannelOverride>> OverrideRowsAsync()
    {
        await using var dbContext = database.CreateSettingsDbContext();
        return await dbContext.MessageLogChannelOverrides.Where(row => row.GuildId == this.GuildId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<bool> MessageSavedAsync(ulong messageId)
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId));
    }

    private Task<JsonNode> WaitForLogAsync(string title)
        => this._bot.WaitForModLogEmbedAsync(embed => embed["author"]?["name"]?.GetValue<string>() == title);

    private async Task<List<T>> HistoryOfAsync<T>(ulong messageId) where T : MessageHistoryEntry
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.MessageHistory.OfType<T>().Where(entry => entry.MessageId == new MessageId(messageId))
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}

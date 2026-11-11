// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Moderation.SpamFilter.Commands;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/SpamFilter Override</c> and <c>/SpamFilter View</c>, the cases <c>SpamFilterTests</c> leaves out (area 04,
///     sections B and C, and XC-89). The filter itself is in <c>SpamFilterTests</c>.
/// </summary>
[Collection("Test collection")]
public sealed class SpamFilterOverrideTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.AntiSpam, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Override ──────────────────────────────────────────────────────────────

    /// <summary>SPAM-43: with no channel, the override is for the channel the command ran in. The reply is public.</summary>
    [Fact]
    public async Task Override_WithNoChannel_IsForTheCurrentChannel()
    {
        var channel = this.Guild.Announcements;

        var reply = await this._bot.RunSlashCommandAndWaitAsync(channel, "spam_filter", this.Guild.Admin,
            SlashOption.Subcommand("override", SlashOption.Enum("option", SpamFilterOverrideSetting.Never)));

        reply.Description.ShouldBe($"Will now never filter spam messages from {channel.Mention} and its sub channels/threads.");
        reply.IsEphemeral.ShouldBeFalse();
        var row = (await this.RowsAsync()).ShouldHaveSingleItem();
        row.ChannelId.ShouldBe(new ChannelId(channel.Id));
        row.ChannelOption.ShouldBe(SpamFilterOverrideOption.NeverFilter);
        row.SetBy.ShouldBe(new ModeratorId(this.Guild.Admin.Id));
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>SPAM-45: a category can be overridden. Whether its channels follow it is SPAM-20, in the filter tests.</summary>
    [Fact]
    public async Task Override_OfACategory_IsAccepted()
    {
        (await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.Category)).Description
            .ShouldBe($"Will now never filter spam messages from {this.Guild.Category.Mention} and its sub channels/threads.");

        (await this.RowsAsync()).ShouldHaveSingleItem().ChannelId.ShouldBe(new ChannelId(this.Guild.Category.Id));
    }

    /// <summary>SPAM-46: setting the value a channel already has replies as a change does, and stores nothing new.</summary>
    [Fact]
    public async Task Override_ToTheValueItAlreadyHas_RepliesTheSame_AndStoresNoNewRow()
    {
        var first = await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.General);

        var second = await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.General);

        second.Description.ShouldNotBeNull().ShouldBe(first.Description);
        second.IsEphemeral.ShouldBeFalse();
        (await this.RowsAsync()).ShouldHaveSingleItem();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>SPAM-47: Inherit on a channel that has no override is already the case, so no row is stored.</summary>
    [Fact]
    public async Task Override_Inherit_OnAChannelWithNoOverride_StoresNoRow()
    {
        (await this.OverrideAsync(SpamFilterOverrideSetting.Inherit, this.Guild.General)).Description
            .ShouldBe($"{this.Guild.General.Mention} and its sub channels/threads will now follow the parent channel's spam filter setting.");

        (await this.RowsAsync()).ShouldBeEmpty();
    }

    /// <summary>SPAM-49: Manage Channels is enough. Manage Guild is not needed.</summary>
    [Fact]
    public async Task Override_ByAMemberWithManageChannelsOnly_IsAllowed()
    {
        var channelManager = this._bot.Rest.CreateRole("Channel Manager", FakeGuild.ManageChannelsPermission);
        this.Guild.SetRoles(this.Guild.User1, channelManager);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("spam_filter", this.Guild.User1,
            SlashOption.Subcommand("override", SlashOption.Enum("option", SpamFilterOverrideSetting.Always),
                SlashOption.Channel("channel", this.Guild.General)));

        reply.Description.ShouldBe($"Will now always filter spam messages from {this.Guild.General.Mention} and its sub channels/threads.");
        var row = (await this.RowsAsync()).ShouldHaveSingleItem();
        row.ChannelOption.ShouldBe(SpamFilterOverrideOption.AlwaysFilter);
        row.SetBy.ShouldBe(new ModeratorId(this.Guild.User1.Id));
    }

    /// <summary>
    ///     SPAM-50, XC-89: with AntiSpam off, Override should refuse the way other module commands do. It has no module
    ///     check, so the override is saved.
    /// </summary>
    [Theory]
    [InlineData(SpamFilterOverrideSetting.Never)]
    [InlineData(SpamFilterOverrideSetting.Always)]
    public async Task Override_WithAntiSpamDisabled_IsRefused(SpamFilterOverrideSetting setting)
    {
        await this._bot.SetModuleAsync(Module.AntiSpam, false);

        (await this.OverrideAsync(setting, this.Guild.General)).Description
            .ShouldBe("This module is disabled in this server.");

        (await this.RowsAsync()).ShouldBeEmpty();
    }

    /// <summary>SPAM-40, KI-60: Override posts to the mod log, as every other settings change that saves does.</summary>
    [Fact]
    public async Task Override_PostsToTheModLog()
    {
        await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.General);

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} set the spam filter override of {this.Guild.General.Mention} to Never.");
    }

    /// <summary>
    ///     SPAM-53: the bot doesn't need to see the channel, because Override makes no Discord call for it. The bot
    ///     runs without Administrator, so <c>#private</c>'s deny of View Channel applies to it.
    /// </summary>
    [Fact]
    public async Task Override_OfAChannelTheBotCannotSee_IsAccepted()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database,
            guild => guild.BotPermissions = FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);

        (await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.Private)).Description
            .ShouldBe($"Will now never filter spam messages from {this.Guild.Private.Mention} and its sub channels/threads.");

        (await this.RowsAsync()).ShouldHaveSingleItem().ChannelId.ShouldBe(new ChannelId(this.Guild.Private.Id));
    }

    // ── View ──────────────────────────────────────────────────────────────────

    /// <summary>SPAM-61: with no overrides, View should say "None", as other views do. It shows an empty embed.</summary>
    [Fact]
    public async Task View_WithNoOverrides_SaysNone()
    {
        var view = await this.ViewAsync();

        view.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Spam Filter Override Settings");
        view.Description.ShouldNotBeNull().ShouldContain("None");
    }

    /// <summary>SPAM-62: a channel set back to Inherit is no longer listed. Another override shows View still lists.</summary>
    [Fact]
    public async Task View_LeavesOutAChannelSetBackToInherit()
    {
        await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.General);
        await this.OverrideAsync(SpamFilterOverrideSetting.Always, this.Guild.Forum);
        await this.OverrideAsync(SpamFilterOverrideSetting.Inherit, this.Guild.General);

        var description = (await this.ViewAsync()).Description.ShouldNotBeNull();

        description.ShouldContain($"{this.Guild.Forum.Mention} - Always Filter");
        description.ShouldNotContain(this.Guild.General.Mention);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>SPAM-63: a deleted channel's override is left out of View, but its row stays.</summary>
    [Fact]
    public async Task View_LeavesOutADeletedChannel_ButKeepsItsRow()
    {
        await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.Announcements);
        await this.OverrideAsync(SpamFilterOverrideSetting.Always, this.Guild.Forum);
        (await this.ViewAsync()).Description.ShouldNotBeNull().ShouldContain($"{this.Guild.Announcements.Mention} - Never Filter");

        this._bot.DeleteChannel(this.Guild.Announcements);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        var description = (await this.ViewAsync()).Description.ShouldNotBeNull();
        description.ShouldContain($"{this.Guild.Forum.Mention} - Always Filter");
        description.ShouldNotContain(this.Guild.Announcements.Mention);
        (await this.RowsAsync()).ShouldContain(row => row.ChannelId == new ChannelId(this.Guild.Announcements.Id));
    }

    /// <summary>
    ///     SPAM-66: KI-60 put the module check on the whole command group, so View is refused with AntiSpam off too, as
    ///     the Log and Ignore views are with their modules off.
    /// </summary>
    [Fact]
    public async Task View_WithAntiSpamDisabled_IsRefused()
    {
        await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.General);
        await this._bot.SetModuleAsync(Module.AntiSpam, false);

        (await this.ViewAsync()).Description.ShouldBe("This module is disabled in this server.");
    }

    /// <summary>SPAM-54: one guild's overrides are not listed in another guild.</summary>
    [Fact]
    public async Task View_InAnotherGuild_DoesNotListThisGuildsOverrides()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database, secondGuild: true);
        var second = this._bot.SecondGuild!;
        await this._bot.SetModuleAsync(Module.AntiSpam, true, second);
        await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.General);
        await this._bot.RunSlashCommandAndWaitAsync(second.General, "spam_filter", second.Admin,
            SlashOption.Subcommand("override", SlashOption.Enum("option", SpamFilterOverrideSetting.Always),
                SlashOption.Channel("channel", second.General)));

        var secondView = await this._bot.RunSlashCommandAndWaitAsync(second.General, "spam_filter", second.Admin,
            SlashOption.Subcommand("view"));
        var firstView = await this.ViewAsync();

        secondView.Description.ShouldNotBeNull().ShouldContain($"{second.General.Mention} - Always Filter");
        secondView.Description.ShouldNotContain(this.Guild.General.Mention);
        firstView.Description.ShouldNotBeNull().ShouldContain($"{this.Guild.General.Mention} - Never Filter");
        firstView.Description.ShouldNotContain(second.General.Mention);
    }

    private Task<FakeReply> OverrideAsync(SpamFilterOverrideSetting setting, FakeChannel channel)
        => this._bot.RunSlashCommandAndWaitAsync("spam_filter", this.Guild.Admin,
            SlashOption.Subcommand("override", SlashOption.Enum("option", setting), SlashOption.Channel("channel", channel)));

    private Task<FakeReply> ViewAsync()
        => this._bot.RunSlashCommandAndWaitAsync("spam_filter", this.Guild.Admin, SlashOption.Subcommand("view"));

    private async Task<List<SpamFilterOverride>> RowsAsync()
    {
        await using var settingsContext = database.CreateSettingsDbContext();
        return await settingsContext.SpamFilterOverrides.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Moderation.SpamFilter.Commands;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     The spam filter (<c>SpamEvents</c>, <c>SpamTrackerModule</c>) and <c>/SpamFilter</c> Override and View.
///     Four identical messages in a row score over the threshold. The filter mutes with a System actor, which can't be
///     saved (KI-31), so the "is muted" tests are skipped. The "not filtered" tests also assert no errors, because a
///     flagged message currently logs one.
/// </summary>
[Collection("Test collection")]
public sealed class SpamFilterTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan HandlersSettle = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;
    private ulong _muteRoleId;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        this._muteRoleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(this._muteRoleId))).ShouldSucceed();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── The filter ────────────────────────────────────────────────────────────

    /// <summary>A spammer is muted for a minute, their message removed, and everyone told.</summary>
    [Fact(Skip = "Fails: the filter records the mute with a System actor, which can't be saved, so spam is never acted on. KI-31")]
    public async Task Spam_MutesTheSpammer_DeletesTheMessage_AndLogs()
    {
        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
                                                            && request.Path.EndsWith($"/members/{this.Guild.User1.Id}/roles/{this._muteRoleId}", StringComparison.Ordinal));
        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Delete
                                                            && request.Path.StartsWith($"channels/{this.Guild.General.Id}/messages/", StringComparison.Ordinal));
        await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Mute");
        await using var dbContext = this._database.CreateDbContext();
        var sin = await dbContext.Sins.SingleAsync(TestContext.Current.CancellationToken);
        sin.Actor.ShouldBe(new ModerationActor.System());
    }

    /// <summary>GEN-90: the filter still acts with the AntiSpam module off, because it never reads that flag.</summary>
    [Fact(Skip = "Blocked: whether AntiSpam is honoured can't be seen until spam is acted on at all. KI-31, then GEN-90")]
    public async Task Spam_WithAntiSpamDisabled_IsIgnored()
    {
        await this._bot.SetModuleAsync(Module.AntiSpam, false);

        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this.ShouldNotBeFilteredAsync();
    }

    /// <summary>A few ordinary messages stay under the threshold.</summary>
    [Fact]
    public async Task OrdinaryMessages_AreNotFiltered()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "how is everyone");

        await this.ShouldNotBeFilteredAsync();
    }

    /// <summary>Members with Manage Channels, and the owner, are never filtered.</summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("owner")]
    public async Task Staff_AreNeverFiltered(string who)
    {
        await this.SpamAsync(who == "admin" ? this.Guild.Admin : this.Guild.Owner, this.Guild.General);

        await this.ShouldNotBeFilteredAsync();
    }

    /// <summary>A Never override on a channel, or on a thread's parent, turns the filter off there.</summary>
    [Theory]
    [InlineData("channel")]
    [InlineData("parent")]
    public async Task NeverOverride_TurnsTheFilterOff(string where)
    {
        var overridden = where == "channel" ? this.Guild.General : this.Guild.Forum;
        (await this.Settings.SetSpamFilterOverrideAsync(new ChannelId(overridden.Id), this.GuildId, new ModeratorId(this.Guild.Owner.Id), SpamFilterOverrideOption.NeverFilter, TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();

        await this.SpamAsync(this.Guild.User1, where == "channel" ? this.Guild.General : this.Guild.ForumPost);

        await this.ShouldNotBeFilteredAsync();
    }

    /// <summary>With Moderation off, or no mute role, there's nothing to mute with, so nothing happens.</summary>
    [Theory]
    [InlineData("moderation off")]
    [InlineData("no mute role")]
    public async Task WithoutAMuteRole_SpamIsIgnored(string situation)
    {
        if (situation == "moderation off")
            await this._bot.SetModuleAsync(Module.Moderation, false);
        else
            (await this.Settings.DisableMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();

        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this.ShouldNotBeFilteredAsync();
    }

    // ── /SpamFilter ───────────────────────────────────────────────────────────

    /// <summary>Always and Never are stored and confirmed, and View lists them.</summary>
    [Fact]
    public async Task Override_StoresTheSetting_AndViewListsIt()
    {
        (await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.General)).Description
            .ShouldBe($"Will now never filter spam messages from {this.Guild.General.Mention} and its sub channels/threads.");
        (await this.OverrideAsync(SpamFilterOverrideSetting.Always, this.Guild.Forum)).Description
            .ShouldBe($"Will now always filter spam messages from {this.Guild.Forum.Mention} and its sub channels/threads.");

        var view = await this._bot.RunSlashCommandAndWaitAsync("spam_filter", this.Guild.Admin, SlashOption.Subcommand("view"));

        view.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Spam Filter Override Settings");
        view.Description.ShouldNotBeNull().ShouldContain($"{this.Guild.General.Mention} - Never Filter");
        view.Description.ShouldContain($"{this.Guild.Forum.Mention} - Always Filter");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>Inherit clears an override, and should say so.</summary>
    [Fact(Skip = "Fails: the setting is saved, then the reply's switch throws ArgumentOutOfRangeException for Inherit, " +
                 "so the moderator sees an error. KI-37")]
    public async Task Override_Inherit_ClearsTheOverrideAndSaysSo()
    {
        await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.General);

        (await this.OverrideAsync(SpamFilterOverrideSetting.Inherit, this.Guild.General)).Description
            .ShouldNotBeNull().ShouldContain(this.Guild.General.Mention);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>Threads aren't guild channels, so they can't be overridden directly.</summary>
    [Fact]
    public async Task Override_OfAThread_SaysTheChannelDoesNotExist()
        => (await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.PublicThread)).Description
            .ShouldBe("That channel does not exist in this server.");

    [Theory]
    [InlineData("override")]
    [InlineData("view")]
    public async Task SpamFilter_NeedsManageChannels(string subcommand)
    {
        var options = subcommand == "view"
            ? SlashOption.Subcommand("view")
            : SlashOption.Subcommand("override", SlashOption.Enum("option", SpamFilterOverrideSetting.Never));

        (await this._bot.RunSlashCommandAndWaitAsync("spam_filter", this.Guild.Moderator, options)).Description
            .ShouldBe("You do not have the required permissions to use this command.");
    }

    private Task<FakeReply> OverrideAsync(SpamFilterOverrideSetting setting, FakeChannel channel)
        => this._bot.RunSlashCommandAndWaitAsync("spam_filter", this.Guild.Admin,
            SlashOption.Subcommand("override", SlashOption.Enum("option", setting), SlashOption.Channel("channel", channel)));

    /// <summary>Six identical messages in a row, well over the threshold.</summary>
    private async Task SpamAsync(FakeUser user, FakeChannel channel)
    {
        for (var i = 0; i < 6; i++)
        {
            this._bot.SendMessage(user, channel, "buy cheap gold now");
            await Task.Delay(TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);
        }
    }

    private async Task ShouldNotBeFilteredAsync()
    {
        await Task.Delay(HandlersSettle, TestContext.Current.CancellationToken);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        this._bot.Rest.Requests.Count(request => request.Method == HttpMethod.Put && request.Path.Contains("/roles/", StringComparison.Ordinal))
            .ShouldBe(0);
        this._bot.Logs.Errors.ShouldBeEmpty("a flagged message currently logs an error (KI-31)");
    }
}

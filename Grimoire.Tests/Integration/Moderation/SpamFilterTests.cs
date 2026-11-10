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
        await this._bot.SetModuleAsync(Module.AntiSpam, true);
        this._muteRoleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(this._muteRoleId))).ShouldSucceed();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── The filter ────────────────────────────────────────────────────────────

    /// <summary>A spammer is muted for a minute, their message removed, and everyone told.</summary>
    /// <remarks>Cases: SPAM-01, SPAM-02.</remarks>
    [Fact]
    public async Task Spam_MutesTheSpammer_DeletesTheMessage_AndLogs()
    {
        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
                                                            && request.Path.EndsWith($"/members/{this.Guild.User1.Id}/roles/{this._muteRoleId}", StringComparison.Ordinal));
        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Delete
                                                            && request.Path.StartsWith($"channels/{this.Guild.General.Id}/messages/", StringComparison.Ordinal));
        await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Mute");
        // KI-53: one burst is one mute, however many of its messages were still being handled.
        await Task.Delay(HandlersSettle, TestContext.Current.CancellationToken);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.ToListAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem()
            .Actor.ShouldBe(new ModerationActor.System());
        await using var settingsContext = this._database.CreateSettingsDbContext();
        (await settingsContext.Mutes.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    /// <summary>
    ///     M-6: deleting the spam is the step most likely to fail, as without Manage Messages or when PluralKit got
    ///     there first. It must not cost the member their DM or the mods their log.
    /// </summary>
    /// <remarks>Cases: SPAM-25.</remarks>
    [Fact]
    public async Task Spam_WhoseMessageCannotBeDeleted_IsStillLoggedAndTold()
    {
        this._bot.Rest.On(HttpMethod.Delete, @"channels/\d+/messages/\d+",
            _ => FakeDiscordRest.Error(System.Net.HttpStatusCode.NotFound, 10008, "Unknown Message"));

        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Mute");
        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                            && request.Path == $"channels/{this.Guild.User1.Id + 1000}/messages");
    }

    /// <summary>
    ///     Attachments cost the same however many there are, because Discord shows several images as one gallery. Two
    ///     quick messages of ten images each used to add up to spam.
    /// </summary>
    /// <remarks>Cases: SPAM-08.</remarks>
    [Fact]
    public async Task ImagesSharedTogether_AreNotSpam()
    {
        var images = Enumerable.Range(1, 10).Select(i => $"photo{i}.png").ToArray();

        for (var i = 0; i < 2; i++)
        {
            this._bot.SendMessage(this.Guild.User1, this.Guild.General, $"holiday pictures {i}", images);
            await Task.Delay(TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);
        }

        await this.ShouldNotBeFilteredAsync();
    }

    /// <summary>GEN-90: with the AntiSpam module off, the filter leaves everyone alone.</summary>
    /// <remarks>Cases: SPAM-15, XC-84.</remarks>
    [Fact]
    public async Task Spam_WithAntiSpamDisabled_IsIgnored()
    {
        await this._bot.SetModuleAsync(Module.AntiSpam, false);

        await this.SpamAsync(this.Guild.User1, this.Guild.General);

        await this.ShouldNotBeFilteredAsync();
    }

    /// <summary>A few ordinary messages stay under the threshold.</summary>
    /// <remarks>Cases: SPAM-07.</remarks>
    [Fact]
    public async Task OrdinaryMessages_AreNotFiltered()
    {
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "hello");
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "how is everyone");

        await this.ShouldNotBeFilteredAsync();
    }

    /// <summary>Members with Manage Channels, and the owner, are never filtered.</summary>
    /// <remarks>Cases: SPAM-12.</remarks>
    [Theory]
    [InlineData("admin")]
    [InlineData("owner")]
    public async Task Staff_AreNeverFiltered(string who)
    {
        await this.SpamAsync(who == "admin" ? this.Guild.Admin : this.Guild.Owner, this.Guild.General);

        await this.ShouldNotBeFilteredAsync();
    }

    /// <summary>A Never override on a channel, or on a thread's parent, turns the filter off there.</summary>
    /// <remarks>Cases: SPAM-18 (channel), SPAM-19 (parent), XC-96.</remarks>
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
    /// <remarks>Cases: SPAM-16 (moderation off), SPAM-17 (no mute role).</remarks>
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
    /// <remarks>Cases: SPAM-40, SPAM-41, SPAM-60.</remarks>
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
    /// <remarks>Cases: SPAM-42.</remarks>
    [Fact]
    public async Task Override_Inherit_ClearsTheOverrideAndSaysSo()
    {
        await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.General);

        (await this.OverrideAsync(SpamFilterOverrideSetting.Inherit, this.Guild.General)).Description
            .ShouldNotBeNull().ShouldContain(this.Guild.General.Mention);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>M-10: a thread can be overridden too. The filter honours it, and the reply promises it.</summary>
    /// <remarks>Cases: SPAM-44.</remarks>
    [Fact]
    public async Task Override_OfAThread_IsAccepted()
        => (await this.OverrideAsync(SpamFilterOverrideSetting.Never, this.Guild.PublicThread)).Description
            .ShouldBe($"Will now never filter spam messages from {this.Guild.PublicThread.Mention} and its sub channels/threads.");

    /// <remarks>Cases: SPAM-48 (override), SPAM-65 (view), XC-63.</remarks>
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

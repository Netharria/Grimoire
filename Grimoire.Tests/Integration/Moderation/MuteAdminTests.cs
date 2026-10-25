// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/Mutes</c> View, Set, Create and Refresh, cases MOD-69 to MOD-89 in
///     <c>docs/testing/02-moderation-actions.md</c>. After a public defer, each progress reply edits the same original
///     response, so tests read the sequence of edits. The diagnostics follow-up's contents are in A-20, and MOD-89
///     (two guilds) in A-35.
/// </summary>
[Collection("Test collection")]
public sealed class MuteAdminTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string PermissionError = "You do not have the required permissions to use this command.";
    private const ulong SendMessages = 1UL << 11;
    private const ulong SendTtsMessages = 1UL << 12;
    private const ulong AddReactions = 1UL << 6;
    private const ulong SendMessagesInThreads = 1UL << 38;
    private const ulong Connect = 1UL << 20;
    private const ulong Speak = 1UL << 21;
    private const ulong AttachFiles = 1UL << 15;
    private const ulong TextLock = SendMessages | SendMessagesInThreads | SendTtsMessages | AddReactions;
    private const ulong VoiceLock = Connect | Speak;

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
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── View ──────────────────────────────────────────────────────────────────

    /// <summary>MOD-69.</summary>
    [Fact]
    public async Task View_WithNothingConfigured_ShowsNoneForBoth_Ephemerally()
    {
        var reply = await this.MutesAsync(this.Guild.Admin, "view");

        reply.IsEphemeral.ShouldBeTrue();
        Field(reply.Embed!, "Mute Role").ShouldBe("None");
        Field(reply.Embed!, "Muted Users").ShouldBe("None");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>MOD-70, MOD-71 and MOD-76: the gate is Manage Guild, with owner and Administrator bypasses.</summary>
    [Theory]
    [InlineData("mod", "view", false)]
    [InlineData("mod", "set", false)]
    [InlineData("owner", "view", true)]
    [InlineData("admin", "view", true)]
    public async Task Mutes_NeedsManageGuild(string who, string subcommand, bool allowed)
    {
        var user = who switch { "owner" => this.Guild.Owner, "admin" => this.Guild.Admin, _ => this.Guild.Moderator };
        var roleId = this._bot.Rest.CreateRole("Muted");

        var reply = subcommand == "set"
            ? await this._bot.RunSlashCommandAndWaitAsync("mutes", user, SlashOption.Subcommand("set", SlashOption.Role("role", roleId)))
            : await this.MutesAsync(user, subcommand);

        (reply.Description == PermissionError).ShouldBe(!allowed);
        if (!allowed)
            (await this.Settings.GetConfiguredMuteRole(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeNull();
    }

    /// <summary>MOD-72.</summary>
    [Fact]
    public async Task Mutes_WithModerationDisabled_IsRefused()
    {
        await this._bot.SetModuleAsync(Module.Moderation, false);

        (await this.MutesAsync(this.Guild.Admin, "view")).Description.ShouldBe("This module is disabled in this server.");
    }

    /// <summary>MOD-74.</summary>
    [Fact]
    public async Task View_ShowsTheConfiguredRole()
    {
        var roleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(roleId), TestContext.Current.CancellationToken)).ShouldSucceed();

        Field((await this.MutesAsync(this.Guild.Admin, "view")).Embed!, "Mute Role").ShouldBe($"<@&{roleId}>");
    }

    /// <summary>MOD-78: a configured role deleted in Discord shows as None.</summary>
    [Fact]
    public async Task View_WithTheConfiguredRoleDeleted_ShowsNone()
    {
        var roleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(roleId), TestContext.Current.CancellationToken)).ShouldSucceed();
        this._bot.Rest.DeleteRole(roleId);

        var mutesRole = await GrimoireTestBot.EventuallyAsync(
            async () => Field((await this.MutesAsync(this.Guild.Admin, "view")).Embed!, "Mute Role"), value => value == "None");
        mutesRole.ShouldBe("None");
    }

    /// <summary>KI-14 area: an active mute should be listed.</summary>
    [Fact(Skip = "Fails: View maps each mute to a Task and then filters for DiscordMember, which a Task never is, so " +
                 "Muted Users is always None. KI-26")]
    public async Task View_ListsMutedMembers()
    {
        await this.Settings.AddMute(new UserId(this.Guild.User1.Id), this.GuildId, new ModeratorId(this.Guild.Moderator.Id), new SinId(1), DateTimeOffset.UtcNow.AddHours(1), null, TestContext.Current.CancellationToken);

        Field((await this.MutesAsync(this.Guild.Admin, "view")).Embed!, "Muted Users").ShouldBe(this.Guild.User1.MemberMention);
    }

    // ── Set ───────────────────────────────────────────────────────────────────

    /// <summary>MOD-73.</summary>
    [Fact]
    public async Task Set_SavesTheRoleAndLogsIt()
    {
        var roleId = this._bot.Rest.CreateRole("Muted");

        var reply = await this._bot.RunSlashCommandAndWaitAsync("mutes", this.Guild.Admin,
            SlashOption.Subcommand("set", SlashOption.Role("role", roleId)));

        reply.Description.ShouldBe($"Will now use role <@&{roleId}> for muting users.");
        (await this.Settings.GetConfiguredMuteRole(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBe(new RoleId(roleId));
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} updated the mute role to <@&{roleId}>");
    }

    /// <summary>MOD-75: any role is accepted, including @everyone and roles above the bot (KI-13). Pins current behaviour.</summary>
    [Theory]
    [InlineData("everyone")]
    [InlineData("higher")]
    public async Task Set_AcceptsAnyRoleWithoutValidation(string which)
    {
        var roleId = which == "everyone" ? this.Guild.Id : this.Guild.HigherRoleId;

        await this._bot.RunSlashCommandAndWaitAsync("mutes", this.Guild.Admin, SlashOption.Subcommand("set", SlashOption.Role("role", roleId)));

        (await this.Settings.GetConfiguredMuteRole(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBe(new RoleId(roleId));
    }

    /// <summary>MOD-77 and MOD-88: every subcommand needs the bot to hold Manage Roles.</summary>
    [Theory]
    [InlineData("set")]
    [InlineData("create")]
    public async Task WhenTheBotLacksManageRoles_IsRefused(string subcommand)
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this._bot.ConfigureModerationAsync();
        var roleId = this._bot.Rest.CreateRole("Muted");

        var reply = subcommand == "set"
            ? await this._bot.RunSlashCommandAndWaitAsync("mutes", this.Guild.Admin, SlashOption.Subcommand("set", SlashOption.Role("role", roleId)))
            : await this.MutesAsync(this.Guild.Admin, "create");

        reply.Description.ShouldBe("The bot did not have the needed permissions to execute this command.");
        (await this.Settings.GetConfiguredMuteRole(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeNull();
        this._bot.Rest.Requests.Count(request => request.Method == HttpMethod.Post && request.Path.EndsWith("/roles", StringComparison.Ordinal))
            .ShouldBe(0);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    /// <summary>MOD-79 and MOD-80: the role, the setting, the overwrites on each channel type, the log and diagnostics.</summary>
    [Fact]
    public async Task Create_MakesTheRole_SavesIt_DeniesEachChannel_AndFollowsUpWithDiagnostics()
    {
        var interaction = this._bot.RunSlashCommand("mutes", this.Guild.Admin, SlashOption.Subcommand("create"));

        await this._bot.Rest.WaitForRequestAsync(request => request.Path.Contains($"/{interaction.Token}", StringComparison.Ordinal)
                                                            && request.Method == HttpMethod.Post && request.Path.StartsWith("webhooks/", StringComparison.Ordinal),
            TimeSpan.FromSeconds(15));
        var roleId = (await this.Settings.GetConfiguredMuteRole(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed()!.Value.Value;
        var bot = $"<@{this.Guild.Bot.Id}>";

        this.EditsOf(interaction).ShouldBe([
            $"Role <@&{roleId}> is created. Now Saving role to {bot} configuration.",
            $"Role <@&{roleId}> is saved in {bot} configuration. Now setting role permissions",
            $"Successfully created role <@&{roleId}> and set permissions for channels"
        ]);
        this._bot.Rest.Requests.Single(request => request.Method == HttpMethod.Post && request.Path.EndsWith("/roles", StringComparison.Ordinal))
            .Json!["name"]!.GetValue<string>().ShouldBe("Muted");

        foreach (var channel in new[] { this.Guild.General, this.Guild.ModLog, this.Guild.Forum, this.Guild.Private, this.Guild.Category })
            this.Guild.OverwriteOf(channel.Id, roleId).ShouldBe((0UL, TextLock), channel.Name);
        foreach (var channel in new[] { this.Guild.Voice, this.Guild.Stage })
            this.Guild.OverwriteOf(channel.Id, roleId).ShouldBe((0UL, VoiceLock), channel.Name);
        this.Guild.OverwriteOf(this.Guild.Announcements.Id, roleId).ShouldBeNull("announcement channels are skipped");
        this.Guild.OverwriteOf(this.Guild.PublicThread.Id, roleId).ShouldBeNull("threads inherit from their parent");

        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} created <@&{roleId}> to use as a mute role.");
    }

    /// <summary>MOD-81: a channel the bot can't see should be listed, without failing the whole command.</summary>
    [Fact(Skip = "Fails: RetryDiscordCall's fallback overload neither awaits the call nor catches a 403, so one refused " +
                 "overwrite fails the whole command instead of being listed. KI-27")]
    public async Task Create_ListsChannelsItCouldNotSet()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission
            | FakeGuild.ManageRolesPermission);
        await this._bot.ConfigureModerationAsync();
        var interaction = this._bot.RunSlashCommand("mutes", this.Guild.Admin, SlashOption.Subcommand("create"));

        var edits = await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.EditsOf(interaction)), found => found.Count >= 3);

        edits[^1].ShouldContain(this.Guild.Private.Mention);
        (await this.Settings.GetConfiguredMuteRole(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed().ShouldNotBeNull();
    }

    /// <summary>MOD-83: a second Create makes another "Muted" role and switches the setting to it (KI-15).</summary>
    [Fact]
    public async Task Create_Twice_MakesASecondRole_AndSwitchesToIt()
    {
        await this.CreateAndWaitAsync();
        var first = (await this.Settings.GetConfiguredMuteRole(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed()!.Value;

        await this.CreateAndWaitAsync();

        var second = (await this.Settings.GetConfiguredMuteRole(this.GuildId, TestContext.Current.CancellationToken)).ShouldSucceed()!.Value;
        second.ShouldNotBe(first);
        this.Guild.HasRole(first.Value).ShouldBeTrue("the first role is left behind");
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    /// <summary>MOD-84 and MOD-85.</summary>
    [Theory]
    [InlineData(false, "No mute role is configured.")]
    [InlineData(true, "Could not find configured mute role.")]
    public async Task Refresh_WithoutAUsableRole_SaysWhy(bool configureThenDelete, string expected)
    {
        if (configureThenDelete)
        {
            var roleId = this._bot.Rest.CreateRole("Muted");
            (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(roleId), TestContext.Current.CancellationToken)).ShouldSucceed();
            this._bot.Rest.DeleteRole(roleId);
            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }

        (await this.MutesAsync(this.Guild.Admin, "refresh")).Description.ShouldBe(expected);
    }

    /// <summary>MOD-82 and MOD-86: Refresh fixes an existing overwrite, keeps unrelated bits, and covers new channels.</summary>
    [Fact]
    public async Task Refresh_FixesExistingOverwrites_AndKeepsUnrelatedBits()
    {
        var roleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, new ModeratorId(this.Guild.Owner.Id), new RoleId(roleId), TestContext.Current.CancellationToken)).ShouldSucceed();
        this.Guild.SetOverwrite(this.Guild.General.Id, roleId, 0, SendMessages | AttachFiles, 0);
        this._bot.Gateway.Dispatch("CHANNEL_UPDATE", this.Guild.ChannelJson(this.Guild.General));
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        var interaction = this._bot.RunSlashCommand("mutes", this.Guild.Admin, SlashOption.Subcommand("refresh"));
        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                            && request.Path.EndsWith($"/{interaction.Token}", StringComparison.Ordinal),
            TimeSpan.FromSeconds(15));

        this.Guild.OverwriteOf(this.Guild.General.Id, roleId).ShouldBe((AttachFiles, TextLock));
        this.Guild.OverwriteOf(this.Guild.ModLog.Id, roleId).ShouldBe((0UL, TextLock));
        this.EditsOf(interaction)[0].ShouldBe($"Refreshing permissions for <@&{roleId}> role.");
        // MOD-86 records the typo "Succussfully"; KI-28 tracks it.
        this.EditsOf(interaction)[^1].ShouldBe($"Succussfully refreshed permissions for <@&{roleId}> role.");
    }

    private Task<FakeReply> MutesAsync(FakeUser user, string subcommand)
        => this._bot.RunSlashCommandAndWaitAsync("mutes", user, SlashOption.Subcommand(subcommand));

    private async Task CreateAndWaitAsync()
    {
        var interaction = this._bot.RunSlashCommand("mutes", this.Guild.Admin, SlashOption.Subcommand("create"));
        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                            && request.Path.EndsWith($"/{interaction.Token}", StringComparison.Ordinal),
            TimeSpan.FromSeconds(15));
    }

    /// <summary>The descriptions of every edit to the interaction's original response, in order.</summary>
    private List<string> EditsOf(FakeInteraction interaction)
        => [.. this._bot.Rest.Requests.Where(interaction.IsOriginalResponseEdit)
            .Select(request => request.Json?["embeds"]?[0]?["description"]?.GetValue<string>() ?? "")];

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

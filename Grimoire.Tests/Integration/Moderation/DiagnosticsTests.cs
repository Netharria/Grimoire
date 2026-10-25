// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/Diagnostics</c> Check and Ignore (Add, Remove, View), and the report that follows <c>/Mutes Create</c>.
///     The analysis itself is unit tested in <c>Features/Moderation/Diagnostics/</c>; these tests cover the commands,
///     the attached report file and the stored ignore list.
/// </summary>
[Collection("Test collection")]
public sealed class DiagnosticsTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string ReportFile = "moderation-diagnostics.txt";

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

    /// <summary>Check replies privately with a summary embed and the full report as a text file.</summary>
    [Fact]
    public async Task Check_RepliesPrivately_WithTheReportAttached()
    {
        var interaction = this._bot.RunSlashCommand("diagnostics", this.Guild.Admin, SlashOption.Subcommand("check"));

        var reply = await FakeReply.WaitForAsync(this._bot.Rest, interaction);

        reply.IsEphemeral.ShouldBeTrue();
        reply.Embed!["title"]!.GetValue<string>().ShouldBe("Moderation diagnostics");
        this._bot.Rest.Requests.Single(interaction.IsOriginalResponseEdit).Files[ReportFile].ShouldNotBeNullOrWhiteSpace();
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>The report that follows /Mutes Create carries the same file.</summary>
    [Fact]
    public async Task MutesCreate_FollowsUpWithTheReport()
    {
        var interaction = this._bot.RunSlashCommand("mutes", this.Guild.Admin, SlashOption.Subcommand("create"));

        var followUp = await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                                           && request.Path.EndsWith($"/{interaction.Token}", StringComparison.Ordinal),
            TimeSpan.FromSeconds(15));

        followUp.Json!["embeds"]![0]!["title"]!.GetValue<string>().ShouldBe("Moderation diagnostics");
        followUp.Files[ReportFile].ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Add stores roles and members, confirms them, and logs it.</summary>
    [Fact]
    public async Task IgnoreAdd_StoresRolesAndMembers_AndLogs()
    {
        var reply = await this.IgnoreAsync("add",
            SlashOption.MentionableRole("items_0", this.Guild.ModeratorRoleId), SlashOption.MentionableUser("items_1", this.Guild.User1));

        var expected = $"<@&{this.Guild.ModeratorRoleId}> <@{this.Guild.User1.Id}> are now ignored by the diagnostics.";
        reply.Description.ShouldBe(expected);
        var stored = await this.StoredIgnoresAsync();
        stored.OfType<DiagnosticsIgnoredRole>().Select(role => role.RoleId).ShouldBe([new RoleId(this.Guild.ModeratorRoleId)]);
        stored.OfType<DiagnosticsIgnoredMember>().Select(member => member.UserId).ShouldBe([new UserId(this.Guild.User1.Id)]);
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>() == $"{this.Guild.Admin.MemberMention}: {expected}");
    }

    /// <summary>@everyone can't be ignored, and with nothing else given there's nothing to do.</summary>
    [Fact]
    public async Task IgnoreAdd_OfOnlyEveryone_Warns()
    {
        (await this.IgnoreAsync("add", SlashOption.MentionableRole("items_0", this.Guild.Id))).Description
            .ShouldBe("Only roles and members can be ignored. Administrator roles are always left out and @everyone cannot be.");
        (await this.StoredIgnoresAsync()).ShouldBeEmpty();
    }

    /// <summary>Adding what's already ignored, or removing what isn't, says so.</summary>
    [Fact]
    public async Task IgnoreAddAgain_AndRemoveOfSomethingNotIgnored_Explain()
    {
        await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1));

        (await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1))).Description
            .ShouldBe("Those roles and members are already ignored.");
        (await this.IgnoreAsync("remove", SlashOption.MentionableUser("items_0", this.Guild.Moderator))).Description
            .ShouldBe("None of those roles or members are ignored.");
    }

    /// <summary>Remove takes items off the list, and View shows what's left.</summary>
    [Fact]
    public async Task IgnoreRemove_TakesThemOff_AndViewShowsTheList()
    {
        await this.IgnoreAsync("add",
            SlashOption.MentionableRole("items_0", this.Guild.ModeratorRoleId), SlashOption.MentionableUser("items_1", this.Guild.User1));

        (await this.IgnoreAsync("remove", SlashOption.MentionableUser("items_0", this.Guild.User1))).Description
            .ShouldBe($"<@{this.Guild.User1.Id}> is no longer ignored by the diagnostics.");

        var view = await this._bot.RunSlashCommandAndWaitAsync("diagnostics", this.Guild.Admin,
            SlashOption.Group("ignore", SlashOption.Subcommand("view")));
        view.Embed!["title"]!.GetValue<string>().ShouldBe("Ignored by the diagnostics");
        var fields = view.Embed!["fields"]!.AsArray().ToDictionary(f => f!["name"]!.GetValue<string>(), f => f!["value"]!.GetValue<string>());
        fields["Roles"].ShouldBe($"<@&{this.Guild.ModeratorRoleId}>");
        fields["Members"].ShouldBe("None");
    }

    /// <summary>Every subcommand needs Manage Guild and the Moderation module.</summary>
    [Theory]
    [InlineData("permission")]
    [InlineData("module")]
    public async Task Diagnostics_NeedsManageGuildAndTheModule(string missing)
    {
        if (missing == "module")
            await this._bot.SetModuleAsync(Module.Moderation, false);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("diagnostics", missing == "module" ? this.Guild.Admin : this.Guild.Moderator,
            SlashOption.Subcommand("check"));

        reply.Description.ShouldBe(missing == "module"
            ? "This module is disabled in this server."
            : "You do not have the required permissions to use this command.");
    }

    private Task<FakeReply> IgnoreAsync(string subcommand, params SlashOption[] items)
        => this._bot.RunSlashCommandAndWaitAsync("diagnostics", this.Guild.Admin,
            SlashOption.Group("ignore", SlashOption.Subcommand(subcommand, items)));

    private async Task<FrozenSet<DiagnosticsIgnoredItem>> StoredIgnoresAsync()
        => (await this.Settings.GetDiagnosticsIgnoredItemsAsync(this.GuildId)).ShouldSucceed();
}

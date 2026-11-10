// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     <c>/Diagnostics Ignore Add|Remove|View</c>, cases DIAG-51 to DIAG-70 in
///     <c>docs/testing/04-spamfilter-diagnostics.md</c>. The basic add, remove and view cases are in
///     <see cref="DiagnosticsTests" />; these cover the mixed inputs, the gates, the list's limits and failures.
/// </summary>
[Collection("Test collection")]
public sealed partial class DiagnosticsIgnoreTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private const string PermissionError = "You do not have the required permissions to use this command.";
    private const string ModuleDisabledError = "This module is disabled in this server.";
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>DIAG-51: one item gets the singular reply, one row and a mod log line.</summary>
    [Fact]
    public async Task Add_OfOneItem_SaysIs_AndStoresOneRow()
    {
        var reply = await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1));

        var expected = $"{this.Guild.User1.Mention} is now ignored by the diagnostics.";
        reply.Description.ShouldBe(expected);
        reply.IsEphemeral.ShouldBeFalse();
        (await this.StoredAsync()).Single().ShouldBeOfType<DiagnosticsIgnoredMember>().SetBy
            .ShouldBe(new ModeratorId(this.Guild.Admin.Id));
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>() == $"{this.Guild.Admin.MemberMention}: {expected}");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>DIAG-53: @everyone is skipped, the rest is added, and the reply says how many were skipped.</summary>
    [Fact]
    public async Task Add_WithEveryoneAmongOthers_SkipsIt_AndSaysSo()
    {
        var reply = await this.IgnoreAsync("add",
            SlashOption.MentionableRole("items_0", this.Guild.Id), SlashOption.MentionableRole("items_1", this.Guild.ModeratorRoleId));

        reply.Description.ShouldBe($"<@&{this.Guild.ModeratorRoleId}> is now ignored by the diagnostics.\n"
                                   + "Skipped 1 item(s) that are not roles or members.");
        (await this.StoredAsync()).Single().ShouldBeOfType<DiagnosticsIgnoredRole>().RoleId
            .ShouldBe(new RoleId(this.Guild.ModeratorRoleId));
    }

    /// <summary>DIAG-55: only what isn't ignored yet is added and named.</summary>
    [Fact]
    public async Task Add_WithOneAlreadyIgnored_AddsAndNamesOnlyTheNewOne()
    {
        await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1));

        var reply = await this.IgnoreAsync("add",
            SlashOption.MentionableUser("items_0", this.Guild.User1), SlashOption.MentionableUser("items_1", this.Guild.Higher));

        reply.Description.ShouldBe($"{this.Guild.Higher.Mention} is now ignored by the diagnostics.");
        (await this.StoredAsync()).OfType<DiagnosticsIgnoredMember>().Select(member => member.UserId.Value)
            .ShouldBe([this.Guild.User1.Id, this.Guild.Higher.Id], ignoreOrder: true);
    }

    /// <summary>DIAG-56: the same member twice in one call is added once.</summary>
    [Fact]
    public async Task Add_OfTheSameMemberTwice_AddsItOnce()
    {
        var reply = await this.IgnoreAsync("add",
            SlashOption.MentionableUser("items_0", this.Guild.User1), SlashOption.MentionableUser("items_1", this.Guild.User1));

        reply.Description.ShouldBe($"{this.Guild.User1.Mention} is now ignored by the diagnostics.");
        (await this.StoredAsync()).Count.ShouldBe(1);
    }

    /// <summary>DIAG-57: an Administrator role is accepted and saved, though the report lists it only once.</summary>
    [Fact]
    public async Task Add_OfAnAdministratorRole_IsAccepted()
    {
        var reply = await this.IgnoreAsync("add", SlashOption.MentionableRole("items_0", this.Guild.AdminRoleId));

        reply.Description.ShouldBe($"<@&{this.Guild.AdminRoleId}> is now ignored by the diagnostics.");
        (await this.StoredAsync()).Single().ShouldBeOfType<DiagnosticsIgnoredRole>().RoleId.ShouldBe(new RoleId(this.Guild.AdminRoleId));
    }

    /// <summary>DIAG-60: removing only @everyone is a private warning, with no mod log.</summary>
    [Fact]
    public async Task Remove_OfOnlyEveryone_Warns()
    {
        var reply = await this.IgnoreAsync("remove", SlashOption.MentionableRole("items_0", this.Guild.Id));

        reply.Description.ShouldBe("Only roles and members can be on the ignore list.");
        reply.IsEphemeral.ShouldBeTrue();
        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>DIAG-62: an empty list shows None for both.</summary>
    [Fact]
    public async Task View_OfAnEmptyList_ShowsNoneForBoth()
    {
        var view = await this.IgnoreAsync("view");

        view.IsEphemeral.ShouldBeFalse();
        Fields(view).ShouldBe(new Dictionary<string, string> { ["Roles"] = "None", ["Members"] = "None" });
    }

    /// <summary>DIAG-63 and DIAG-64: every Ignore subcommand needs Manage Guild and the Moderation module.</summary>
    [Theory]
    [InlineData("add", "permission")]
    [InlineData("remove", "permission")]
    [InlineData("view", "permission")]
    [InlineData("add", "module")]
    [InlineData("remove", "module")]
    [InlineData("view", "module")]
    public async Task Ignore_NeedsManageGuildAndTheModule(string subcommand, string missing)
    {
        if (missing == "module")
            await this._bot.SetModuleAsync(Module.Moderation, false);
        SlashOption[] items = subcommand == "view" ? [] : [SlashOption.MentionableUser("items_0", this.Guild.User1)];

        var reply = await this._bot.RunSlashCommandAndWaitAsync("diagnostics",
            missing == "module" ? this.Guild.Admin : this.Guild.Moderator,
            SlashOption.Group("ignore", SlashOption.Subcommand(subcommand, items)));

        reply.Description.ShouldBe(missing == "module" ? ModuleDisabledError : PermissionError);
        reply.IsEphemeral.ShouldBeTrue();
        (await this.StoredAsync()).ShouldBeEmpty();
    }

    /// <summary>DIAG-67: an ignored role deleted in Discord stays on the list, and View still shows it.</summary>
    [Fact]
    public async Task View_StillShowsAnIgnoredRoleThatWasDeleted()
    {
        var roleId = this._bot.Rest.CreateRole("Temporary");
        await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this.Guild.HasRole(roleId)), found => found);
        await this.IgnoreAsync("add", SlashOption.MentionableRole("items_0", roleId));
        this._bot.Rest.DeleteRole(roleId);

        Fields(await this.IgnoreAsync("view"))["Roles"].ShouldBe($"<@&{roleId}>");
        (await this.StoredAsync()).Single().ShouldBeOfType<DiagnosticsIgnoredRole>().RoleId.ShouldBe(new RoleId(roleId));
    }

    /// <summary>
    ///     DIAG-68 (KI-62): with more ignored roles than fit, View cuts the field at 1000 characters, which can split a
    ///     mention and hides how many are left. It should show only whole mentions and say how many more there are.
    /// </summary>
    [Fact(Skip = "Fails: View cuts the Roles field at 1000 characters, splitting a mention and hiding the count. KI-62")]
    public async Task View_OfALongList_ShowsWholeMentions_AndHowManyAreLeft()
    {
        var now = DateTimeOffset.UtcNow;
        var roles = Enumerable.Range(0, 60)
            .Select(DiagnosticsIgnoredItem (index) => DiagnosticsIgnoredRole.Create(
                new RoleId(this.Guild.Id + 10_000 + (ulong)index), this.GuildId, new ModeratorId(this.Guild.Admin.Id), now))
            .ToList();
        (await this.Settings.AddDiagnosticsIgnoredItemsAsync(this.GuildId, roles, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed();

        var text = Fields(await this.IgnoreAsync("view"))["Roles"];

        var shown = WholeRoleMention().Matches(text).Count;
        shown.ShouldBePositive();
        text.Split("<@&").Length.ShouldBe(shown + 1, "every mention shown is whole");
        if (shown < roles.Count)
            text.ShouldContain($"{roles.Count - shown}");
    }

    /// <summary>DIAG-69: when the list can't be saved, Add says so privately and logs nothing.</summary>
    [Fact]
    public async Task Add_WhenTheListCannotBeSaved_SaysSoPrivately()
    {
        await using (await database.RefuseSettingsInsertsAsync(nameof(DiagnosticsIgnoredItem)))
        {
            var reply = await this.IgnoreAsync("add", SlashOption.MentionableUser("items_0", this.Guild.User1));

            reply.Description.ShouldBe("Could not save the diagnostics ignore list.");
            reply.IsEphemeral.ShouldBeTrue();
        }

        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
        (await this.StoredAsync()).ShouldBeEmpty();
    }

    /// <summary>DIAG-70: each guild has its own list.</summary>
    [Fact]
    public async Task View_InAnotherGuild_ShowsThatGuildsList()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database, secondGuild: true);
        await this._bot.ConfigureModerationAsync();
        var second = this._bot.SecondGuild!;
        await this._bot.ConfigureModerationAsync(second);
        await this.IgnoreAsync("add", SlashOption.MentionableRole("items_0", this.Guild.ModeratorRoleId));

        var view = await this._bot.RunSlashCommandAndWaitAsync(second.General, "diagnostics", this.Guild.Admin,
            SlashOption.Group("ignore", SlashOption.Subcommand("view")));

        Fields(view)["Roles"].ShouldBe("None");
        Fields(await this.IgnoreAsync("view"))["Roles"].ShouldBe($"<@&{this.Guild.ModeratorRoleId}>");
    }

    private Task<FakeReply> IgnoreAsync(string subcommand, params SlashOption[] items)
        => this._bot.RunSlashCommandAndWaitAsync("diagnostics", this.Guild.Admin,
            SlashOption.Group("ignore", SlashOption.Subcommand(subcommand, items)));

    private async Task<FrozenSet<DiagnosticsIgnoredItem>> StoredAsync()
    {
        await using var context = database.CreateSettingsDbContext();
        return (await context.DiagnosticsIgnoredItems.AsNoTracking().ToListAsync()).ToFrozenSet();
    }

    private static Dictionary<string, string> Fields(FakeReply reply)
        => reply.Embed!["fields"]!.AsArray()
            .ToDictionary(field => field!["name"]!.GetValue<string>(), field => field!["value"]!.GetValue<string>());

    [GeneratedRegex(@"<@&\d+>")]
    private static partial Regex WholeRoleMention();
}

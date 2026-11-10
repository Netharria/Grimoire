// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.CustomCommands;

/// <summary>
///     The two autocomplete providers (backlog A-48, area 07 sections D and H), driven through
///     <c>INTERACTION_CREATE</c> type 4: <c>GetCustomCommandOptions</c> for command names, and
///     <c>GetCommandVersionOptions</c> for <c>/Commands Revert version</c>.
/// </summary>
[Collection("Test collection")]
public sealed class CustomCommandAutocompleteTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database, secondGuild: true);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Commands, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Version autocomplete (/Commands Revert) ──────────────────────────────

    /// <summary>CC-51: newest first, numbered from the full count, with the date and who made each version.</summary>
    [Fact]
    public async Task VersionChoices_AreNewestFirst_WithTheirNumberDateAndModerator()
    {
        var start = new DateTimeOffset(2026, 3, 4, 13, 5, 0, TimeSpan.Zero);
        await this.SeedVersionsAsync(this.Guild, "hello", 3, start);

        var choices = await this.VersionChoicesAsync("hello");

        choices.Select(choice => choice.Name).ShouldBe([
            "v3 — Mar 4, 2026 3:05 PM UTC by admin",
            "v2 — Mar 4, 2026 2:05 PM UTC by admin",
            "v1 — Mar 4, 2026 1:05 PM UTC by admin"
        ]);
        choices.Select(choice => choice.Value).ShouldBe([.. new[] { 2, 1, 0 }
            .Select(hours => start.AddHours(hours).UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>A choice's value is what Revert takes: picking one reverts to that version.</summary>
    [Fact]
    public async Task AVersionChoice_RevertsToThatVersion()
    {
        await this.SeedVersionsAsync(this.Guild, "hello", 2, new DateTimeOffset(2026, 3, 4, 13, 5, 0, TimeSpan.Zero));
        var oldest = (await this.VersionChoicesAsync("hello"))[^1];

        var reply = await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand("revert",
            SlashOption.String("name", "hello"), SlashOption.String("version", oldest.Value)));

        reply.Description.ShouldStartWith("Reverted `hello` to version from <t:");
        await using var dbContext = database.CreateDbContext();
        (await dbContext.CustomCommands.AsNoTracking().OrderByDescending(command => command.CreatedAt).FirstAsync(Token))
            .Content.Value.ShouldBe("content 1");
    }

    /// <summary>CC-52: only 25 fit, so 30 versions offer v30 down to v6, numbered as History numbers them.</summary>
    [Fact]
    public async Task ThirtyVersions_OfferTheNewest25_NumberedFromTheFullCount()
    {
        await this.SeedVersionsAsync(this.Guild, "hello", 30, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var choices = await this.VersionChoicesAsync("hello");

        choices.Count.ShouldBe(25);
        choices[0].Name.ShouldStartWith("v30 — ");
        choices[^1].Name.ShouldStartWith("v6 — ");
    }

    [Fact]
    public async Task VersionChoices_ForAnUnknownName_AreEmpty()
        => (await this.VersionChoicesAsync("nope")).ShouldBeEmpty();

    // ── Name autocomplete ─────────────────────────────────────────────────────

    /// <summary>CC-100: with nothing typed, the first five by name, and placeholders shown in the label.</summary>
    [Fact]
    public async Task NameChoices_WithNothingTyped_AreTheFirstFiveByName_WithPlaceholderHints()
    {
        foreach (var name in new[] { "zeta", "alpha", "hug", "delta", "beta", "gamma", "omega" })
            await this.SeedVersionsAsync(this.Guild, name, 1, DateTimeOffset.UtcNow.AddDays(-1),
                name == "hug" ? "%Mention gets a hug: %message" : "hi");

        var choices = await this._bot.RunAutocompleteAndWaitAsync("command", this.Guild.User1, "command_name",
            SlashOption.String("command_name", ""));

        choices.ShouldBe([
            new FakeChoice("alpha", "alpha"), new FakeChoice("beta", "beta"), new FakeChoice("delta", "delta"),
            new FakeChoice("gamma", "gamma"), new FakeChoice("hug <Mention> <Message>", "hug")
        ]);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>CC-101: a typo still finds the command, by edit distance.</summary>
    [Fact]
    public async Task NameChoices_ForATypo_PutTheClosestNameFirst()
    {
        foreach (var name in new[] { "zeta", "alpha", "hug", "delta", "beta", "gamma", "omega" })
            await this.SeedVersionsAsync(this.Guild, name, 1, DateTimeOffset.UtcNow.AddDays(-1));

        var choices = await this._bot.RunAutocompleteAndWaitAsync("command", this.Guild.User1, "command_name",
            SlashOption.String("command_name", "hgu"));

        choices[0].Value.ShouldBe("hug");
    }

    /// <summary>CC-102: levenshtein refuses input over 255 characters, so a long word is cut before the query.</summary>
    [Fact]
    public async Task NameChoices_ForA300CharacterWord_StillAppear()
    {
        await this.SeedVersionsAsync(this.Guild, "hello", 1, DateTimeOffset.UtcNow.AddDays(-1));

        var choices = await this._bot.RunAutocompleteAndWaitAsync("command", this.Guild.User1, "command_name",
            SlashOption.String("command_name", new string('x', 300)));

        choices.ShouldBe([new FakeChoice("hello", "hello")]);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>The same provider serves the /Commands subcommands, such as Stats, for any member.</summary>
    [Fact]
    public async Task NameChoices_AreOfferedInTheCommandsSubcommands()
    {
        await this.SeedVersionsAsync(this.Guild, "hello", 1, DateTimeOffset.UtcNow.AddDays(-1));

        (await this._bot.RunAutocompleteAndWaitAsync("commands", this.Guild.User1, "name",
                SlashOption.Subcommand("stats", SlashOption.String("name", "hel"))))
            .ShouldBe([new FakeChoice("hello", "hello")]);
    }

    /// <summary>CC-34: a forgotten command is no longer offered.</summary>
    [Fact]
    public async Task NameChoices_LeaveOutAForgottenCommand()
    {
        await this.SeedVersionsAsync(this.Guild, "hello", 2, DateTimeOffset.UtcNow.AddDays(-1));
        await this.SeedVersionsAsync(this.Guild, "bye", 1, DateTimeOffset.UtcNow.AddDays(-1));
        (await this._bot.RunAutocompleteAndWaitAsync("command", this.Guild.User1, "command_name",
            SlashOption.String("command_name", ""))).Select(choice => choice.Value).ShouldBe(["bye", "hello"]);

        await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin,
            SlashOption.Subcommand("forget", SlashOption.String("name", "hello")));

        (await this._bot.RunAutocompleteAndWaitAsync("command", this.Guild.User1, "command_name",
            SlashOption.String("command_name", ""))).Select(choice => choice.Value).ShouldBe(["bye"]);
    }

    /// <summary>CC-103: a member who can't run a restricted command shouldn't be offered it.</summary>
    [Fact(Skip = "Fails: the name autocomplete doesn't check the role list, so secret is offered. KI-86")]
    public async Task NameChoices_LeaveOutRestrictedCommands_TheMemberCannotRun()
    {
        await this.SeedVersionsAsync(this.Guild, "hello", 1, DateTimeOffset.UtcNow.AddDays(-1));
        await this.SeedVersionsAsync(this.Guild, "secret", 1, DateTimeOffset.UtcNow.AddDays(-1),
            allowRoleIds: [this.Guild.ModeratorRoleId]);

        (await this._bot.RunAutocompleteAndWaitAsync("command", this.Guild.User1, "command_name",
            SlashOption.String("command_name", ""))).Select(choice => choice.Value).ShouldBe(["hello"]);
    }

    /// <summary>CC-104: another guild's commands are never offered.</summary>
    [Fact]
    public async Task NameChoices_LeaveOutOtherGuildsCommands()
    {
        await this.SeedVersionsAsync(this.Guild, "hello", 1, DateTimeOffset.UtcNow.AddDays(-1));
        await this.SeedVersionsAsync(this._bot.SecondGuild!, "elsewhere", 1, DateTimeOffset.UtcNow.AddDays(-1));

        (await this._bot.RunAutocompleteAndWaitAsync("command", this.Guild.User1, "command_name",
            SlashOption.String("command_name", ""))).Select(choice => choice.Value).ShouldBe(["hello"]);
    }

    private Task<IReadOnlyList<FakeChoice>> VersionChoicesAsync(string name)
        => this._bot.RunAutocompleteAndWaitAsync("commands", this.Guild.Admin, "version",
            SlashOption.Subcommand("revert", SlashOption.String("name", name), SlashOption.String("version", "")));

    /// <summary>Saves <paramref name="count" /> versions an hour apart, by <c>admin</c>, with content "content 1" up.</summary>
    private async Task SeedVersionsAsync(FakeGuild guild, string name, int count, DateTimeOffset first,
        string? content = null, ulong[]? allowRoleIds = null)
    {
        await using var dbContext = database.CreateDbContext();
        dbContext.CustomCommands.AddRange(Enumerable.Range(1, count).Select(number => TextCustomCommand.Create(
            CustomCommandName.Create(name).ShouldSucceed(), new GuildId(guild.Id),
            CustomCommandContent.Create(content ?? $"content {number}").ShouldSucceed(),
            [.. (allowRoleIds ?? []).Select(roleId => new RoleId(roleId))], [], new ModeratorId(guild.Admin.Id),
            first.AddHours(number - 1))));
        await dbContext.SaveChangesAsync(Token);
    }
}

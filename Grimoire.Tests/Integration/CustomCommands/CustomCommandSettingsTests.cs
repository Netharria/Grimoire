// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.CustomCommands;

/// <summary>
///     <c>/Commands</c> Learn, Forget, History, Revert, Stats and Leaderboard (backlog A-29). Each Learn saves a new
///     version, keyed by its creation time. Usage rows are seeded directly; A-30 covers recording them.
/// </summary>
[Collection("Test collection")]
public sealed class CustomCommandSettingsTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private GuildId GuildId => new(this.Guild.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Commands, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Learn ─────────────────────────────────────────────────────────────────

    /// <remarks>Cases: CC-01.</remarks>
    [Fact]
    public async Task Learn_SavesATextCommand_AndLogsIt()
    {
        (await this.LearnAsync("hello", "Hello there!")).Description.ShouldBe("Added hello custom command.");

        var command = (await this.VersionsAsync("hello")).ShouldHaveSingleItem().ShouldBeOfType<TextCustomCommand>();
        command.Content.Value.ShouldBe("Hello there!");
        command.ModeratorId.ShouldBe(new ModeratorId(this.Guild.Admin.Id));
        command.Roles.ShouldBeEmpty();
        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.StartsWith(
            $"{this.Guild.Admin.MemberMention} asked ", StringComparison.Ordinal) == true
            && e["description"]!.GetValue<string>().EndsWith(" to learn a new command: hello", StringComparison.Ordinal));
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <remarks>Cases: CC-02.</remarks>
    [Fact]
    public async Task Learn_WithEmbedAndColor_SavesAnEmbedCommand()
    {
        await this.LearnAsync("rules", "Be nice", SlashOption.Boolean("embed", true), SlashOption.String("embed_color", "ff00aa"));

        (await this.VersionsAsync("rules")).ShouldHaveSingleItem().ShouldBeOfType<EmbedCustomCommand>()
            .EmbedColor!.Value.Value.ShouldBe("FF00AA");
    }

    /// <remarks>Cases: CC-07.</remarks>
    [Fact]
    public async Task Learn_Restricted_SavesItsRoles()
    {
        await this.LearnAsync("secret", "shh", SlashOption.Boolean("restricted_use", true),
            SlashOption.Role("permission_role_0", this.Guild.KickerRoleId), SlashOption.Role("permission_role_1", this.Guild.BannerRoleId));

        (await this.VersionsAsync("secret")).ShouldHaveSingleItem().Roles.OfType<CustomCommandAllowRole>()
            .Select(role => role.RoleId).ShouldBe([new RoleId(this.Guild.KickerRoleId), new RoleId(this.Guild.BannerRoleId)], ignoreOrder: true);
    }

    /// <remarks>Cases: CC-08.</remarks>
    [Fact]
    public async Task Learn_RestrictedWithoutRoles_IsRejected()
    {
        (await this.LearnAsync("secret", "shh", SlashOption.Boolean("restricted_use", true))).Description
            .ShouldBe("A restricted command must have at least one permission role.");

        (await this.VersionsAsync("secret")).ShouldBeEmpty();
    }

    /// <remarks>Cases: CC-11, CC-40.</remarks>
    [Fact]
    public async Task Learn_AnExistingName_AddsAVersion_ThatHistoryShows()
    {
        await this.LearnAsync("hello", "first");
        await this.LearnAsync("hello", "second\nline");

        var reply = await this.CommandsAsync("history", SlashOption.String("name", "hello"));

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Version history for hello");
        var lines = reply.Description.ShouldNotBeNull().Split('\n');
        lines[0].ShouldStartWith("**v2 (current)** — <t:");
        lines[0].ShouldEndWith($" by {this.Guild.Admin.Mention}");
        lines[1].ShouldBe(@"> second\nline");
        lines[2].ShouldStartWith("**v1** — ");
        lines[3].ShouldBe("> first");
    }

    /// <remarks>Cases: CC-41.</remarks>
    [Fact]
    public async Task History_OfAnUnknownCommand_SaysSo()
        => (await this.CommandsAsync("history", SlashOption.String("name", "nope"))).Description
            .ShouldBe("No command named `nope` exists.");

    // ── Forget ────────────────────────────────────────────────────────────────

    /// <remarks>Cases: CC-30.</remarks>
    [Fact]
    public async Task Forget_DeletesEveryVersionAndItsUsage()
    {
        await this.LearnAsync("hello", "first");
        await this.LearnAsync("hello", "second");
        await this.SeedUsageAsync("hello", (this.Guild.User1, 0));

        (await this.CommandsAsync("forget", SlashOption.String("name", "hello"))).Description.ShouldBe("Removed command hello");

        (await this.VersionsAsync("hello")).ShouldBeEmpty();
        await using var dbContext = database.CreateDbContext();
        (await dbContext.CustomCommandUsages.CountAsync(Token)).ShouldBe(0);
    }

    /// <remarks>Cases: CC-31.</remarks>
    [Fact]
    public async Task Forget_AnUnknownCommand_SaysItWasAlreadyForgotten()
        => (await this.CommandsAsync("forget", SlashOption.String("name", "nope"))).Description
            .ShouldBe("Command `nope` was already forgotten.");

    /// <remarks>Cases: CC-30.</remarks>
    [Fact]
    public async Task Forget_IsLoggedWithTheBotsMention()
    {
        await this.LearnAsync("hello", "first");

        await this.CommandsAsync("forget", SlashOption.String("name", "hello"));

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} asked {this.Guild.Bot.MemberMention} to forget command `hello`.");
    }

    // ── Revert ────────────────────────────────────────────────────────────────

    /// <summary>Reverting saves the old content, and its roles, as a new current version.</summary>
    /// <remarks>Cases: CC-50.</remarks>
    [Fact]
    public async Task Revert_SavesTheOldVersionAsTheNewest()
    {
        await this.LearnAsync("hello", "first", SlashOption.Boolean("restricted_use", true),
            SlashOption.Role("permission_role_0", this.Guild.KickerRoleId));
        await this.LearnAsync("hello", "second");
        var first = (await this.VersionsAsync("hello")).MinBy(version => version.CreatedAt)!;

        var reply = await this.CommandsAsync("revert", SlashOption.String("name", "hello"),
            SlashOption.String("version", first.CreatedAt.UtcTicks.ToString()));

        reply.Description.ShouldBe($"Reverted `hello` to version from <t:{first.CreatedAt.ToUnixTimeSeconds()}:f>.");
        var versions = await this.VersionsAsync("hello");
        versions.Count.ShouldBe(3);
        var newest = versions.MaxBy(version => version.CreatedAt)!;
        newest.Content.Value.ShouldBe("first");
        newest.Roles.OfType<CustomCommandAllowRole>().ShouldHaveSingleItem().RoleId.ShouldBe(new RoleId(this.Guild.KickerRoleId));
    }

    /// <remarks>Cases: CC-53, CC-54.</remarks>
    [Theory]
    [InlineData("abc", "Invalid version selected. Use the autocomplete to pick a version.")]
    [InlineData("12345", "Version not found for command `hello`.")]
    public async Task Revert_ToABadVersion_IsRejected(string version, string error)
    {
        await this.LearnAsync("hello", "first");

        (await this.CommandsAsync("revert", SlashOption.String("name", "hello"), SlashOption.String("version", version)))
            .Description.ShouldBe(error);
        (await this.VersionsAsync("hello")).Count.ShouldBe(1);
    }

    // ── Stats and Leaderboard ─────────────────────────────────────────────────

    /// <remarks>Cases: CC-65.</remarks>
    [Fact]
    public async Task Stats_CountsUsesByPeriod_AndListsTopUsers()
    {
        await this.LearnAsync("hello", "hi");
        await this.SeedUsageAsync("hello", (this.Guild.User1, 0), (this.Guild.User1, 1), (this.Guild.User1, 40), (this.Guild.Moderator, 400));

        var embed = (await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.User1,
            SlashOption.Subcommand("stats", SlashOption.String("name", "hello")))).Embed!;

        embed["author"]!["name"]!.GetValue<string>().ShouldBe("Stats for !hello");
        (Field(embed, "Total Uses"), Field(embed, "Last 30 Days"), Field(embed, "Last Year")).ShouldBe(("4", "2", "3"));
        Field(embed, "Last Used").ShouldStartWith("<t:");
        Field(embed, "Top Users").ShouldBe($"**1.** {this.Guild.User1.Mention} — 3 uses\n**2.** {this.Guild.Moderator.Mention} — 1 uses");
    }

    /// <remarks>Cases: CC-66.</remarks>
    [Fact]
    public async Task Stats_ForAnUnusedCommand_ShowsZeros()
    {
        await this.LearnAsync("hello", "hi");

        var embed = (await this.CommandsAsync("stats", SlashOption.String("name", "hello"))).Embed!;

        Field(embed, "Total Uses").ShouldBe("0");
        Field(embed, "Last Used").ShouldBe("Never");
        Field(embed, "Top Users").ShouldBe("No usage data yet.");
    }

    /// <remarks>Cases: CC-75, CC-76.</remarks>
    [Fact]
    public async Task Leaderboard_RanksCommands_OrUsersOfOneCommand()
    {
        await this.LearnAsync("a", "a");
        await this.LearnAsync("b", "b");
        await this.SeedUsageAsync("a", (this.Guild.User1, 0), (this.Guild.User1, 0), (this.Guild.Moderator, 0));
        await this.SeedUsageAsync("b", (this.Guild.User1, 0));

        var overall = await this.CommandsAsync("leaderboard");
        overall.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Command Leaderboard");
        overall.Description.ShouldBe("**1.** `!a` — 3 uses\n**2.** `!b` — 1 uses");
        overall.Embed!["footer"]!["text"]!.GetValue<string>().ShouldBe("2 commands, 4 uses");

        var forA = await this.CommandsAsync("leaderboard", SlashOption.String("name", "a"));
        forA.Description.ShouldBe($"**1.** {this.Guild.User1.Mention} — 2 uses\n**2.** {this.Guild.Moderator.Mention} — 1 uses");
        forA.Embed!["footer"]!["text"]!.GetValue<string>().ShouldBe("3 total uses");
    }

    /// <remarks>Cases: CC-77.</remarks>
    [Fact]
    public async Task Leaderboard_WithNoUsage_SaysSo()
        => (await this.CommandsAsync("leaderboard")).Description.ShouldBe("No commands have been used yet.");

    // ── Gates ─────────────────────────────────────────────────────────────────

    /// <remarks>Cases: CC-22, CC-24.</remarks>
    [Theory]
    [InlineData("permission")]
    [InlineData("module")]
    public async Task Learn_NeedsManageGuildAndTheModule(string missing)
    {
        if (missing == "module")
            await this._bot.SetModuleAsync(Module.Commands, false);

        (await this._bot.RunSlashCommandAndWaitAsync("commands", missing == "module" ? this.Guild.Admin : this.Guild.Moderator,
                SlashOption.Subcommand("learn", SlashOption.String("name", "hello"), SlashOption.String("content", "hi"))))
            .Description.ShouldBe(missing == "module"
                ? "This module is disabled in this server."
                : "You do not have the required permissions to use this command.");
        (await this.VersionsAsync("hello")).ShouldBeEmpty();
    }

    /// <summary>C-4: Learn says what's wrong with a name or colour, instead of "Failed to parse name."</summary>
    /// <remarks>Cases: CC-05, CC-06, CC-13.</remarks>
    [Theory]
    [InlineData("two words", null, false, "A command name cannot contain spaces.")]
    [InlineData("hello", "not-a-colour", true, "A command embed color must be a 6 character hex color code.")]
    [InlineData("hello", "ff0000", false, "An embed color only applies to an embed. Set embed to true, or leave out the color.")]
    public async Task Learn_WithABadNameOrColour_SaysWhatIsWrong(string name, string? colour, bool embed, string error)
    {
        var reply = await this.LearnAsync(name, "hi",
        [
            SlashOption.Boolean("embed", embed),
            .. colour is null ? Array.Empty<SlashOption>() : [SlashOption.String("embed_color", colour)]
        ]);

        reply.Description.ShouldBe(error);
        await using var dbContext = database.CreateDbContext();
        (await dbContext.CustomCommands.AnyAsync(Token)).ShouldBeFalse();
    }

    /// <summary>C-4: with the name a plain string again, Discord's client enforces its length.</summary>
    /// <remarks>Cases: CC-14.</remarks>
    [Fact]
    public void Learn_RegistersTheNameLength()
    {
        var name = this._bot.Rest.RegisteredCommands["commands"]["options"]!.AsArray()
            .Single(option => option!["name"]!.GetValue<string>() == "learn")!["options"]!.AsArray()
            .Single(option => option!["name"]!.GetValue<string>() == "name")!;

        (name["min_length"]!.GetValue<int>(), name["max_length"]!.GetValue<int>()).ShouldBe((1, 24));
    }

    /// <summary>
    ///     The variadic <c>permission_role</c> registers ten optional role options, <c>permission_role_0</c> to
    ///     <c>permission_role_9</c>.
    /// </summary>
    [Fact]
    public void Learn_RegistersTenOptionalPermissionRoles()
    {
        var roles = this._bot.Rest.RegisteredCommands["commands"]["options"]!.AsArray()
            .Single(option => option!["name"]!.GetValue<string>() == "learn")!["options"]!.AsArray()
            .Where(option => option!["name"]!.GetValue<string>().StartsWith("permission_role", StringComparison.Ordinal))
            .ToList();

        roles.Select(option => option!["name"]!.GetValue<string>())
            .ShouldBe(Enumerable.Range(0, 10).Select(index => $"permission_role_{index}"));
        roles.ShouldAllBe(option => option!["type"]!.GetValue<int>() == 8
                                    && (option["required"] == null || !option["required"]!.GetValue<bool>()));
    }

    /// <summary>C-8: forgetting a command that isn't there changes nothing, so the mod log hears nothing.</summary>
    /// <remarks>Cases: CC-31.</remarks>
    [Fact]
    public async Task Forget_AnUnknownCommand_LogsNothing()
    {
        await this._bot.ConfigureModerationAsync();

        await this.CommandsAsync("forget", SlashOption.String("name", "nope"));

        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, TimeSpan.FromSeconds(2))).ShouldBeFalse();
    }

    /// <summary>C-8: stats for a name that was never created say so, as History does, instead of showing zeros.</summary>
    /// <remarks>Cases: CC-67.</remarks>
    [Fact]
    public async Task Stats_ForAnUnknownCommand_SaysSo()
        => (await this.CommandsAsync("stats", SlashOption.String("name", "nope"))).Description
            .ShouldBe("No command named `nope` exists.");

    /// <summary>C-8: a name that was never created isn't "not used yet".</summary>
    /// <remarks>Cases: CC-78.</remarks>
    [Fact]
    public async Task Leaderboard_ForAnUnknownCommand_SaysItDoesNotExist()
        => (await this.CommandsAsync("leaderboard", SlashOption.String("name", "nope"))).Description
            .ShouldBe("No command named `nope` exists.");

    /// <summary>C-8: the footers count everything, not only the fifteen rows shown.</summary>
    /// <remarks>Cases: CC-80.</remarks>
    [Fact]
    public async Task Leaderboard_Footers_CountPastTheFifteenShown()
    {
        var users = Enumerable.Range(0, 20).Select(i => new FakeUser(1_100_000_000_000_011_000 + (ulong)i, $"u{i}")).ToArray();
        await this.LearnAsync("hello", "hi");
        await this.SeedUsageAsync("hello", [.. users.Select(user => (user, 1))]);
        foreach (var i in Enumerable.Range(0, 20))
            await this.SeedUsageAsync($"cmd{i}", (this.Guild.User1, 1));

        (await this.CommandsAsync("leaderboard")).Embed!["footer"]!["text"]!.GetValue<string>().ShouldBe("21 commands, 40 uses");
        (await this.CommandsAsync("leaderboard", SlashOption.String("name", "hello"))).Embed!["footer"]!["text"]!
            .GetValue<string>().ShouldBe("20 total uses");
    }

    private Task<FakeReply> CommandsAsync(string subcommand, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand(subcommand, options));

    private Task<FakeReply> LearnAsync(string name, string content, params SlashOption[] options)
        => this.CommandsAsync("learn", [SlashOption.String("name", name), SlashOption.String("content", content), .. options]);

    private async Task<List<CustomCommand>> VersionsAsync(string name)
    {
        await using var dbContext = database.CreateDbContext();
        var commandName = CustomCommandName.Create(name).ShouldSucceed();
        return await dbContext.CustomCommands.AsNoTracking().Include(command => command.Roles)
            .Where(command => command.GuildId == this.GuildId && command.Name == commandName).ToListAsync(Token);
    }

    private async Task SeedUsageAsync(string name, params (FakeUser User, int DaysAgo)[] uses)
    {
        await using var seed = database.CreateDbContext();
        seed.CustomCommandUsages.AddRange(uses.Select(use => new CustomCommandUsage
        {
            Name = CustomCommandName.Create(name).ShouldSucceed(),
            GuildId = this.GuildId,
            UserId = new UserId(use.User.Id),
            UsedAt = DateTimeOffset.UtcNow.AddDays(-use.DaysAgo).AddSeconds(-1)
        }));
        await seed.SaveChangesAsync(Token);
    }

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

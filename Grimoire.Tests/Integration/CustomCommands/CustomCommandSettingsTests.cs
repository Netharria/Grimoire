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
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private GuildId GuildId => new(this.Guild.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Commands, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Learn ─────────────────────────────────────────────────────────────────

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

    [Fact]
    public async Task Learn_WithEmbedAndColor_SavesAnEmbedCommand()
    {
        await this.LearnAsync("rules", "Be nice", SlashOption.Boolean("embed", true), SlashOption.String("embed_color", "ff00aa"));

        (await this.VersionsAsync("rules")).ShouldHaveSingleItem().ShouldBeOfType<EmbedCustomCommand>()
            .EmbedColor!.Value.Value.ShouldBe("FF00AA");
    }

    [Fact]
    public async Task Learn_Restricted_SavesItsRoles()
    {
        await this.LearnAsync("secret", "shh", SlashOption.Boolean("restricted_use", true),
            SlashOption.Role("permission_role_1", this.Guild.KickerRoleId), SlashOption.Role("permission_role_2", this.Guild.BannerRoleId));

        (await this.VersionsAsync("secret")).ShouldHaveSingleItem().Roles.OfType<CustomCommandAllowRole>()
            .Select(role => role.RoleId).ShouldBe([new RoleId(this.Guild.KickerRoleId), new RoleId(this.Guild.BannerRoleId)], ignoreOrder: true);
    }

    [Fact]
    public async Task Learn_RestrictedWithoutRoles_IsRejected()
    {
        (await this.LearnAsync("secret", "shh", SlashOption.Boolean("restricted_use", true))).Description
            .ShouldBe("A restricted command must have at least one permission role.");

        (await this.VersionsAsync("secret")).ShouldBeEmpty();
    }

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

    [Fact]
    public async Task History_OfAnUnknownCommand_SaysSo()
        => (await this.CommandsAsync("history", SlashOption.String("name", "nope"))).Description
            .ShouldBe("No command named `nope` exists.");

    // ── Forget ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Forget_DeletesEveryVersionAndItsUsage()
    {
        await this.LearnAsync("hello", "first");
        await this.LearnAsync("hello", "second");
        await this.SeedUsageAsync("hello", (this.Guild.User1, 0));

        (await this.CommandsAsync("forget", SlashOption.String("name", "hello"))).Description.ShouldBe("Removed command hello");

        (await this.VersionsAsync("hello")).ShouldBeEmpty();
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.CustomCommandUsages.CountAsync(Token)).ShouldBe(0);
    }

    [Fact]
    public async Task Forget_AnUnknownCommand_SaysItWasAlreadyForgotten()
        => (await this.CommandsAsync("forget", SlashOption.String("name", "nope"))).Description
            .ShouldBe("Command `nope` was already forgotten.");

    [Fact(Skip = "Fails: Forget's mod log interpolates the bot's member object instead of its mention, so it reads " +
                 "\"asked Member 1100…; Grimoire#0 to forget\". KI-47")]
    public async Task Forget_IsLoggedWithTheBotsMention()
    {
        await this.LearnAsync("hello", "first");

        await this.CommandsAsync("forget", SlashOption.String("name", "hello"));

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Guild.Admin.MemberMention} asked {this.Guild.Bot.MemberMention} to forget command `hello`.");
    }

    // ── Revert ────────────────────────────────────────────────────────────────

    /// <summary>Reverting saves the old content, and its roles, as a new current version.</summary>
    [Fact]
    public async Task Revert_SavesTheOldVersionAsTheNewest()
    {
        await this.LearnAsync("hello", "first", SlashOption.Boolean("restricted_use", true),
            SlashOption.Role("permission_role_1", this.Guild.KickerRoleId));
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

    [Fact]
    public async Task Stats_ForAnUnusedCommand_ShowsZeros()
    {
        await this.LearnAsync("hello", "hi");

        var embed = (await this.CommandsAsync("stats", SlashOption.String("name", "hello"))).Embed!;

        Field(embed, "Total Uses").ShouldBe("0");
        Field(embed, "Last Used").ShouldBe("Never");
        Field(embed, "Top Users").ShouldBe("No usage data yet.");
    }

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
        overall.Embed!["footer"]!["text"]!.GetValue<string>().ShouldBe("2 commands");

        var forA = await this.CommandsAsync("leaderboard", SlashOption.String("name", "a"));
        forA.Description.ShouldBe($"**1.** {this.Guild.User1.Mention} — 2 uses\n**2.** {this.Guild.Moderator.Mention} — 1 uses");
        forA.Embed!["footer"]!["text"]!.GetValue<string>().ShouldBe("3 total uses");
    }

    [Fact]
    public async Task Leaderboard_WithNoUsage_SaysSo()
        => (await this.CommandsAsync("leaderboard")).Description.ShouldBe("No commands have been used yet.");

    // ── Gates ─────────────────────────────────────────────────────────────────

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

    private Task<FakeReply> CommandsAsync(string subcommand, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand(subcommand, options));

    private Task<FakeReply> LearnAsync(string name, string content, params SlashOption[] options)
        => this.CommandsAsync("learn", [SlashOption.String("name", name), SlashOption.String("content", content), .. options]);

    private async Task<List<CustomCommand>> VersionsAsync(string name)
    {
        await using var dbContext = this._database.CreateDbContext();
        var commandName = CustomCommandName.Create(name).ShouldSucceed();
        return await dbContext.CustomCommands.AsNoTracking().Include(command => command.Roles)
            .Where(command => command.GuildId == this.GuildId && command.Name == commandName).ToListAsync(Token);
    }

    private async Task SeedUsageAsync(string name, params (FakeUser User, int DaysAgo)[] uses)
    {
        await using var seed = this._database.CreateDbContext();
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

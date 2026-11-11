// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.CustomCommands;

/// <summary>
///     <c>/Commands</c> Forget, History, Revert, Stats and Leaderboard cases past what
///     <see cref="CustomCommandSettingsTests" /> covers (backlog A-48, area 07 sections B to F).
/// </summary>
[Collection("Test collection")]
public sealed class CustomCommandManageGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

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

    // ── Forget ────────────────────────────────────────────────────────────────

    /// <summary>CC-32: forgetting a restricted command removes its roles too.</summary>
    [Fact]
    public async Task Forget_ARestrictedCommand_RemovesItsRoles()
    {
        await this.LearnAsync("secret", "shh", SlashOption.Boolean("restricted_use", true),
            SlashOption.Role("permission_role_0", this.Guild.KickerRoleId));

        (await this.CommandsAsync("forget", SlashOption.String("name", "secret"))).Description.ShouldBe("Removed command secret");

        await using var dbContext = database.CreateDbContext();
        (await dbContext.CustomCommands.CountAsync(Token)).ShouldBe(0);
        (await dbContext.Set<CustomCommandRole>().CountAsync(Token)).ShouldBe(0);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>CC-34: a forgotten command no longer runs from text.</summary>
    [Fact]
    public async Task Forget_ThenTheTextTrigger_PostsNothing()
    {
        await this.LearnAsync("hello", "Hello there!");
        await this.CommandsAsync("forget", SlashOption.String("name", "hello"));

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!hello");

        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Post
                                                                    && request.Path == $"channels/{this.Guild.General.Id}/messages",
            NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>CC-35: Forget's name goes through the name converter, so a space is a DSharpPlus parse error.</summary>
    [Fact]
    public async Task Forget_ANameWithASpace_IsAParseError()
        => (await this.CommandsAsync("forget", SlashOption.String("name", "two words"))).Description
            .ShouldBe("Failed to parse name.");

    // ── History ───────────────────────────────────────────────────────────────

    /// <summary>CC-42: previews over 100 characters are cut with an ellipsis.</summary>
    [Fact]
    public async Task History_CutsLongPreviews()
    {
        await this.LearnAsync("long", new string('a', 100) + new string('b', 50));

        var lines = (await this.CommandsAsync("history", SlashOption.String("name", "long"))).Description!.Split('\n');

        lines[1].ShouldBe("> " + new string('a', 100) + "…");
    }

    /// <summary>CC-44: a legacy version with no moderator shows no "by" part.</summary>
    [Fact]
    public async Task History_OfALegacyVersionWithoutAModerator_LeavesOutBy()
    {
        await using (var seed = database.CreateDbContext())
        {
            seed.CustomCommands.Add(TextCustomCommand.Create(CustomCommandName.Create("old").ShouldSucceed(), this.GuildId,
                CustomCommandContent.Create("legacy").ShouldSucceed(), [], [], new ModeratorId(this.Guild.Admin.Id),
                DateTimeOffset.UtcNow.AddDays(-1)) with { ModeratorId = null });
            await seed.SaveChangesAsync(Token);
        }

        var lines = (await this.CommandsAsync("history", SlashOption.String("name", "old"))).Description!.Split('\n');

        lines[0].ShouldMatch(@"^\*\*v1 \(current\)\*\* — <t:\d+:f>$");
        lines[1].ShouldBe("> legacy");
    }

    // ── Gates ─────────────────────────────────────────────────────────────────

    /// <summary>CC-36, CC-46, CC-60: Forget, History and Revert need Manage Guild, which <c>mod</c> lacks.</summary>
    [Theory]
    [InlineData("forget")]
    [InlineData("history")]
    [InlineData("revert")]
    public async Task ManagingSubcommands_NeedManageGuild(string subcommand)
    {
        await this.LearnAsync("hello", "hi");

        (await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Moderator,
                SlashOption.Subcommand(subcommand, this.OptionsFor(subcommand))))
            .Description.ShouldBe("You do not have the required permissions to use this command.");
        (await this.VersionsAsync("hello")).Count.ShouldBe(1);
    }

    /// <summary>CC-37, CC-46, CC-60, CC-69, CC-82: every subcommand needs the Commands module.</summary>
    [Theory]
    [InlineData("forget")]
    [InlineData("history")]
    [InlineData("revert")]
    [InlineData("stats")]
    [InlineData("leaderboard")]
    public async Task EverySubcommand_NeedsTheModule(string subcommand)
    {
        await this.LearnAsync("hello", "hi");
        await this._bot.SetModuleAsync(Module.Commands, false);

        (await this.CommandsAsync(subcommand, this.OptionsFor(subcommand)))
            .Description.ShouldBe("This module is disabled in this server.");
        (await this.VersionsAsync("hello")).Count.ShouldBe(1);
    }

    // ── Revert ────────────────────────────────────────────────────────────────

    /// <summary>CC-55: a hand-typed version out of DateTimeOffset's range should be "not found", not an exception. KI-83.</summary>
    [Theory]
    [InlineData("9000000000000000000")]
    [InlineData("-1")]
    public async Task Revert_ToAnOutOfRangeVersion_IsNotFound(string version)
    {
        await this.LearnAsync("hello", "first");

        (await this.RevertAsync("hello", version)).Description.ShouldBe("Version not found for command `hello`.");
        (await this.VersionsAsync("hello")).Count.ShouldBe(1);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>CC-56: a version value belongs to one command, so another command's is not found.</summary>
    [Fact]
    public async Task Revert_WithAnotherCommandsVersion_IsNotFound()
    {
        await this.LearnAsync("hello", "first");
        await this.LearnAsync("bye", "later");
        var helloVersion = (await this.VersionsAsync("hello")).Single().CreatedAt;

        (await this.RevertAsync("bye", Ticks(helloVersion))).Description.ShouldBe("Version not found for command `bye`.");
        (await this.VersionsAsync("bye")).Count.ShouldBe(1);
    }

    /// <summary>CC-57: reverting to an embed version brings the embed and its colour back.</summary>
    [Fact]
    public async Task Revert_ToAnEmbedVersion_RestoresTheEmbed()
    {
        await this.LearnAsync("rules", "Be nice", SlashOption.Boolean("embed", true), SlashOption.String("embed_color", "ff0000"));
        await this.LearnAsync("rules", "plain now");
        var embedVersion = (await this.VersionsAsync("rules")).MinBy(version => version.CreatedAt)!.CreatedAt;

        await this.RevertAsync("rules", Ticks(embedVersion));

        (await this.VersionsAsync("rules")).MaxBy(version => version.CreatedAt).ShouldBeOfType<EmbedCustomCommand>()
            .EmbedColor!.Value.Value.ShouldBe("FF0000");
        var reply = await this._bot.RunSlashCommandAndWaitAsync("command", this.Guild.User1, SlashOption.String("command_name", "rules"));
        reply.Description.ShouldBe("Be nice");
        reply.Embed!["color"]!.GetValue<int>().ShouldBe(0xFF0000);
        await GrimoireTestBot.EventuallyAsync(this.UseCountAsync, count => count == 1);
    }

    /// <summary>CC-58: reverting to the current version still adds a version with the same content.</summary>
    [Fact]
    public async Task Revert_ToTheCurrentVersion_AddsACopy()
    {
        await this.LearnAsync("hello", "only");
        var current = (await this.VersionsAsync("hello")).Single().CreatedAt;

        (await this.RevertAsync("hello", Ticks(current))).Description.ShouldStartWith("Reverted `hello` to version from <t:");

        (await this.VersionsAsync("hello")).Select(version => version.Content.Value).ShouldBe(["only", "only"]);
    }

    /// <summary>CC-59: reverting a command that doesn't exist says the version isn't found.</summary>
    [Fact]
    public async Task Revert_AnUnknownCommand_IsNotFound()
        => (await this.RevertAsync("nope", "12345")).Description.ShouldBe("Version not found for command `nope`.");

    // ── Stats and Leaderboard ─────────────────────────────────────────────────

    /// <summary>CC-68: a member who can't run a restricted command shouldn't see its stats. KI-86.</summary>
    [Fact]
    public async Task Stats_OfARestrictedCommand_AreHiddenFromMembersWhoCannotRunIt()
    {
        await this.LearnAsync("secret", "shh", SlashOption.Boolean("restricted_use", true),
            SlashOption.Role("permission_role_0", this.Guild.ModeratorRoleId));

        var reply = await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.User1,
            SlashOption.Subcommand("stats", SlashOption.String("name", "secret")));

        reply.Embed?["author"]?["name"]?.GetValue<string>().ShouldNotBe("Stats for !secret");
        reply.Description.ShouldBe("No command named `secret` exists.");
    }

    /// <summary>
    ///     KI-86: the leaderboard leaves out a restricted command for a member who can't run it, overall and by name, but
    ///     a member with Manage Guild still sees it.
    /// </summary>
    [Fact]
    public async Task Leaderboard_OfARestrictedCommand_IsHiddenFromMembersWhoCannotRunIt()
    {
        await this.LearnAsync("secret", "shh", SlashOption.Boolean("restricted_use", true),
            SlashOption.Role("permission_role_0", this.Guild.ModeratorRoleId));
        await this.LearnAsync("hello", "hi");
        await using (var dbContext = database.CreateDbContext())
        {
            string[] used = ["secret", "hello"];
            dbContext.CustomCommandUsages.AddRange(used.Select(name => new CustomCommandUsage
            {
                Name = CustomCommandName.Create(name).ShouldSucceed(), GuildId = this.GuildId,
                UserId = new UserId(this.Guild.Moderator.Id), UsedAt = DateTimeOffset.UtcNow
            }));
            await dbContext.SaveChangesAsync(Token);
        }

        var overall = await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.User1,
            SlashOption.Subcommand("leaderboard"));
        overall.Description.ShouldBe("**1.** `!hello` — 1 uses");
        overall.Embed!["footer"]!["text"]!.GetValue<string>().ShouldBe("1 commands, 1 uses");
        (await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.User1,
                SlashOption.Subcommand("leaderboard", SlashOption.String("name", "secret"))))
            .Description.ShouldBe("No command named `secret` exists.");
        (await this.CommandsAsync("leaderboard")).Description.ShouldNotBeNull().ShouldContain("`!secret`");
    }

    /// <summary>CC-79: the leaderboard of a command nobody has used says so.</summary>
    [Fact]
    public async Task Leaderboard_OfAnUnusedCommand_SaysSo()
    {
        await this.LearnAsync("hello", "hi");

        (await this.CommandsAsync("leaderboard", SlashOption.String("name", "hello"))).Description
            .ShouldBe("Command `!hello` has not been used yet.");
    }

    private SlashOption[] OptionsFor(string subcommand) => subcommand switch
    {
        "leaderboard" => [],
        "revert" => [SlashOption.String("name", "hello"), SlashOption.String("version", "12345")],
        _ => [SlashOption.String("name", "hello")]
    };

    private static string Ticks(DateTimeOffset createdAt) => createdAt.UtcTicks.ToString(CultureInfo.InvariantCulture);

    private Task<FakeReply> RevertAsync(string name, string version)
        => this.CommandsAsync("revert", SlashOption.String("name", name), SlashOption.String("version", version));

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

    private async Task<int> UseCountAsync()
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.CustomCommandUsages.CountAsync(Token);
    }
}

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
///     Using custom commands (backlog A-30): <c>/Command</c>, and <c>!name</c> text triggers through
///     <c>MESSAGE_CREATE</c>. Commands are learned through <c>/Commands Learn</c>. <c>%Mention</c> and
///     <c>%Message</c> are filled from the options or the words after the name, and every use is recorded.
/// </summary>
[Collection("Test collection")]
public sealed class CustomCommandUseTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Commands, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── /Command ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SlashCommand_RepliesWithTheContent_AndRecordsTheUse()
    {
        await this.LearnAsync("hello", "Hello there!");

        var reply = await this._bot.RunSlashCommandAndWaitAsync("command", this.Guild.User1, SlashOption.String("command_name", "hello"));

        reply.Message["content"]!.GetValue<string>().ShouldBe("Hello there!");
        (await this.UsesAsync()).ShouldBe([(this.Guild.User1.Id, "hello")]);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task SlashCommand_FillsMentionAndMessage_AndDefusesEveryone()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");

        var reply = await this._bot.RunSlashCommandAndWaitAsync("command", this.Guild.User1, SlashOption.String("command_name", "hug"),
            SlashOption.MentionableUser("mention", this.Guild.Moderator), SlashOption.String("message", "hi @everyone"));

        reply.Message["content"]!.GetValue<string>()
            .ShouldMatch($"^<@!?{this.Guild.Moderator.Id}> gets a hug: hi @ everyone$");
    }

    [Fact]
    public async Task SlashCommand_AnEmbedCommand_RepliesWithAnEmbed()
    {
        await this.LearnAsync("rules", "Be nice", SlashOption.Boolean("embed", true), SlashOption.String("embed_color", "ff0000"));

        var reply = await this._bot.RunSlashCommandAndWaitAsync("command", this.Guild.User1, SlashOption.String("command_name", "rules"));

        reply.Description.ShouldBe("Be nice");
        reply.Embed!["color"]!.GetValue<int>().ShouldBe(0xFF0000);
    }

    /// <summary>A restricted command works for a member with one of its roles, and silently does nothing otherwise.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SlashCommand_Restricted_OnlyRunsForItsRoles(bool hasRole)
    {
        await this.LearnAsync("secret", "shh", SlashOption.Boolean("restricted_use", true),
            SlashOption.Role("permission_role_1", this.Guild.ModeratorRoleId));
        var invoker = hasRole ? this.Guild.Moderator : this.Guild.User1;

        var interaction = this._bot.RunSlashCommand("command", invoker, SlashOption.String("command_name", "secret"));

        if (hasRole)
            (await this._bot.Rest.WaitForRequestAsync(interaction.IsOriginalResponseEdit)).Json!["content"]!.GetValue<string>().ShouldBe("shh");
        else
        {
            await this._bot.Rest.WaitForRequestAsync(interaction.IsOriginalResponseDelete);
            (await this.UsesAsync()).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task SlashCommand_ThatDoesNotExist_DeletesTheResponse()
    {
        var interaction = this._bot.RunSlashCommand("command", this.Guild.User1, SlashOption.String("command_name", "nope"));

        await this._bot.Rest.WaitForRequestAsync(interaction.IsOriginalResponseDelete);
    }

    // ── !name text triggers ───────────────────────────────────────────────────

    [Fact]
    public async Task TextTrigger_PostsTheContent_AndRecordsTheUse()
    {
        await this.LearnAsync("hello", "Hello there!");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!hello");

        (await this.WaitForPostAsync(this.Guild.General))["content"]!.GetValue<string>().ShouldBe("Hello there!");
        (await GrimoireTestBot.EventuallyAsync(this.UsesAsync, uses => uses.Count == 1)).ShouldBe([(this.Guild.User1.Id, "hello")]);
    }

    [Fact]
    public async Task TextTrigger_FillsMentionFromTheFirstWord_AndMessageFromTheRest()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, $"!hug {this.Guild.Moderator.Mention} you are great");

        (await this.WaitForPostAsync(this.Guild.General))["content"]!.GetValue<string>()
            .ShouldMatch($"^<@!?{this.Guild.Moderator.Id}> gets a hug: you are great$");
    }

    /// <summary>Unknown names, messages from bots, restricted commands and a disabled module post nothing.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("bot")]
    [InlineData("restricted")]
    [InlineData("module off")]
    public async Task TextTriggers_ThatShouldNotRun_PostNothing(string situation)
    {
        await this.LearnAsync("hello", "Hello there!", situation == "restricted"
            ? [SlashOption.Boolean("restricted_use", true), SlashOption.Role("permission_role_1", this.Guild.ModeratorRoleId)]
            : []);
        if (situation == "module off")
            await this._bot.SetModuleAsync(Module.Commands, false);

        this._bot.SendMessage(situation == "bot" ? this.Guild.Bot : this.Guild.User1, this.Guild.General,
            situation == "unknown" ? "!nope" : "!hello");

        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Post
                                                                    && request.Path == $"channels/{this.Guild.General.Id}/messages", NothingHappensWindow))
            .ShouldBeFalse();
        (await this.UsesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task TextTrigger_WhereTheBotCannotPost_IsReported()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this.LearnAsync("hello", "Hello there!");

        this._bot.SendMessage(this.Guild.User1, this.Guild.Private, "!hello");

        await this._bot.WaitForModLogEmbedAsync(e => e["description"]?.GetValue<string>()?.StartsWith(
            $"I could not post in {this.Guild.Private.Mention}.", StringComparison.Ordinal) == true);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    private Task<FakeReply> LearnAsync(string name, string content, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand("learn",
            [SlashOption.String("name", name), SlashOption.String("content", content), .. options]));

    private async Task<System.Text.Json.Nodes.JsonNode> WaitForPostAsync(FakeChannel channel)
        => (await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Post
                                                               && request.Path == $"channels/{channel.Id}/messages")).Json!;

    private async Task<List<(ulong, string)>> UsesAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return [.. (await dbContext.CustomCommandUsages.ToListAsync(Token)).Select(use => (use.UserId.Value, use.Name.Value))];
    }
}

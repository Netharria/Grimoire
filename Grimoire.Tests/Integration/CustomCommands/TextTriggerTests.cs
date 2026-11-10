// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json.Nodes;
using Grimoire.Settings.Enums;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.CustomCommands;

/// <summary>
///     The <c>!name</c> text trigger's parsing and edge cases (backlog A-48, area 07 section I), past what
///     <see cref="CustomCommandUseTests" /> covers.
/// </summary>
[Collection("Test collection")]
public sealed class TextTriggerTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Commands, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>CC-120: leading spaces after the <c>!</c> are dropped.</summary>
    [Fact]
    public async Task ASpaceAfterTheBang_StillRuns()
    {
        await this.LearnAsync("hello", "Hello there!");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "! hello");

        (await this.WaitForPostAsync())["content"]!.GetValue<string>().ShouldBe("Hello there!");
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-121: names are case-sensitive, so <c>!Hello</c> doesn't run <c>hello</c>. KI-87 asks whether that's wanted.</summary>
    [Fact]
    public async Task ADifferentlyCasedName_DoesNotRun()
    {
        await this.LearnAsync("hello", "Hello there!");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!Hello");

        (await this.PostsWithinWindowAsync()).ShouldBeFalse();
    }

    /// <summary>CC-122: only spaces split words, so a newline makes the first word an invalid name.</summary>
    [Fact]
    public async Task ANewlineAfterTheName_DoesNotRun()
    {
        await this.LearnAsync("hello", "Hello there!");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!hello\nmore text");

        (await this.PostsWithinWindowAsync()).ShouldBeFalse();
    }

    /// <summary>CC-123: repeated spaces in the message collapse to one.</summary>
    [Fact]
    public async Task RepeatedSpaces_InTheMessage_Collapse()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, $"!hug {this.Guild.Moderator.Mention} a    b");

        (await this.WaitForPostAsync())["content"]!.GetValue<string>().ShouldEndWith("gets a hug: a b");
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-114: a mention of a user Discord doesn't know isn't resolved, so it stays in the message.</summary>
    [Fact]
    public async Task AnUnknownUserMention_StaysInTheMessage()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!hug <@1> hi");

        (await this.WaitForPostAsync())["content"]!.GetValue<string>().ShouldBe(" gets a hug: <@1> hi");
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-124: a reply to another message counts as a trigger.</summary>
    [Fact]
    public async Task AReplyMessage_Runs()
    {
        await this.LearnAsync("hello", "Hello there!");
        var earlier = this._bot.SendMessage(this.Guild.Moderator, this.Guild.General, "something");

        this.SendReply(this.Guild.User1, earlier, "!hello");

        (await this.WaitForPostAsync())["content"]!.GetValue<string>().ShouldBe("Hello there!");
        await this.WaitForUsesAsync(1);
    }

    /// <summary>
    ///     CC-125: PluralKit reposts a proxied message through a webhook. The webhook copy has no member, so only the
    ///     original runs the command.
    /// </summary>
    [Fact]
    public async Task APluralKitProxy_RunsTheCommandOnce()
    {
        await this.LearnAsync("hello", "Hello there!");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "a:!hello");
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!hello");
        this._bot.SendMessage(new FakeUser(1_100_000_000_000_006_001, "Alice", IsBot: true), this.Guild.General, "!hello");

        await this.WaitForPostAsync();
        await this.WaitForUsesAsync(1);
        (await this.GeneralPostCountAfterWindowAsync()).ShouldBe(1);
        (await this.UsesAsync()).Count.ShouldBe(1);
    }

    /// <summary>CC-20: a name may start with <c>!</c>, and is then triggered with two.</summary>
    [Fact]
    public async Task ANameStartingWithABang_IsTriggeredWithTwo()
    {
        (await this.LearnAsync("!hello", "Hi!")).Description.ShouldBe("Added !hello custom command.");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!!hello");

        (await this.WaitForPostAsync())["content"]!.GetValue<string>().ShouldBe("Hi!");
        (await this.WaitForUsesAsync(1)).ShouldBe([(this.Guild.User1.Id, "!hello")]);
    }

    /// <summary>CC-21 (text path): content with <c>@everyone</c> shows the text and pings nobody.</summary>
    [Fact]
    public async Task EveryoneInTheContent_PingsNobody()
    {
        await this.LearnAsync("look", "@everyone look");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!look");

        var post = await this.WaitForPostAsync();
        post["content"]!.GetValue<string>().ShouldBe("@everyone look");
        ShouldPingNobody(post["allowed_mentions"]);
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-113, as decided in review C-3: a role given as the mention fills %Mention and is pinged.</summary>
    [Fact]
    public async Task ARoleMention_FillsMention_AndPingsThatRole()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, $"!hug <@&{this.Guild.ModeratorRoleId}> hi");

        var post = await this.WaitForPostAsync();
        post["content"]!.GetValue<string>().ShouldBe($"<@&{this.Guild.ModeratorRoleId}> gets a hug: hi");
        // DSharpPlus writes these ids as JSON numbers, not strings.
        post["allowed_mentions"]!["roles"]!.AsArray().Select(role => role!.ToString())
            .ShouldBe([FakeUser.Snowflake(this.Guild.ModeratorRoleId)]);
        await this.WaitForUsesAsync(1);
    }

    /// <summary>
    ///     CC-113, as decided in review C-3: <c>@everyone</c> typed as the message is left as typed, since the sanitiser
    ///     goes, and pings nobody because allowed mentions never include it.
    /// </summary>
    [Fact]
    public async Task EveryoneInTheMessage_IsLeftAsTyped_AndPingsNobody()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!hug @everyone hi");

        var post = await this.WaitForPostAsync();
        post["content"]!.GetValue<string>().ShouldBe(" gets a hug: @everyone hi");
        ShouldPingNobody(post["allowed_mentions"]);
        await this.WaitForUsesAsync(1);
    }

    /// <summary>
    ///     CC-119: without Embed Links, Discord drops the embed and refuses the then-empty message with a 400. That
    ///     should be reported like a channel the bot can't post in, not raised as an error on every use.
    /// </summary>
    [Fact(Skip = "Fails: only UnauthorizedException is caught, so the 400 is logged as an event-handler error. KI-84")]
    public async Task AnEmbedCommand_WithoutEmbedLinks_IsReported_NotRaised()
    {
        await this.LearnAsync("rules", "Be nice", SlashOption.Boolean("embed", true));
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Guild.General.Id}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.BadRequest, 50006, "Cannot send an empty message"));

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!rules");

        await this.WaitForPostAsync();
        (await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this._bot.Logs.Errors.Count), count => count > 0,
            NothingHappensWindow)).ShouldBe(0);
        (await this.UsesAsync()).ShouldBeEmpty();
    }

    /// <summary>
    ///     CC-126: DSharpPlus's own <c>!</c> text processor is registered too. A custom command named like a bot command
    ///     should get one reply, and <c>!commands stats</c> should do nothing. Unskipped, there are three posts: the custom
    ///     command, the leveling <c>/Leaderboard</c>'s parse error for <c>!leaderboard</c>, and <c>/Commands Stats</c>
    ///     running as a text command.
    /// </summary>
    [Fact(Skip = "Fails: DSharpPlus's ! text processor runs /Leaderboard and /Commands Stats as text commands. KI-85")]
    public async Task BotCommandNames_AsTextCommands_OnlyRunTheCustomCommand()
    {
        await this.LearnAsync("leaderboard", "custom board");
        await this.LearnAsync("hello", "hi");

        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!leaderboard");
        (await this.WaitForPostAsync())["content"]!.GetValue<string>().ShouldBe("custom board");
        this._bot.SendMessage(this.Guild.User1, this.Guild.General, "!commands stats hello");

        (await this.GeneralPostCountAfterWindowAsync()).ShouldBe(1);
        (await this.WaitForUsesAsync(1)).ShouldBe([(this.Guild.User1.Id, "leaderboard")]);
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    private static void ShouldPingNobody(JsonNode? allowedMentions)
    {
        allowedMentions.ShouldNotBeNull();
        (allowedMentions["parse"]?.AsArray().Select(kind => kind!.GetValue<string>()) ?? []).ShouldBeEmpty();
        (allowedMentions["users"]?.AsArray() ?? []).ShouldBeEmpty();
        (allowedMentions["roles"]?.AsArray() ?? []).ShouldBeEmpty();
    }

    /// <summary>A reply (message type 19) to <paramref name="repliedTo" />, as Discord sends it.</summary>
    private void SendReply(FakeUser author, ulong repliedTo, string content)
    {
        var channel = this.Guild.General;
        this._bot.Gateway.Dispatch("MESSAGE_CREATE", new JsonObject
        {
            ["id"] = FakeUser.Snowflake(this._bot.NextSnowflake()),
            ["channel_id"] = FakeUser.Snowflake(channel.Id),
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id),
            ["author"] = author.ToJson(),
            ["member"] = FakeGuild.MemberJson(this.Guild.GetMember(author), false),
            ["content"] = content,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["edited_timestamp"] = null,
            ["tts"] = false,
            ["mention_everyone"] = false,
            ["mentions"] = new JsonArray(),
            ["mention_roles"] = new JsonArray(),
            ["attachments"] = new JsonArray(),
            ["embeds"] = new JsonArray(),
            ["components"] = new JsonArray(),
            ["pinned"] = false,
            ["type"] = 19,
            ["flags"] = 0,
            ["message_reference"] = new JsonObject
            {
                ["type"] = 0,
                ["message_id"] = FakeUser.Snowflake(repliedTo),
                ["channel_id"] = FakeUser.Snowflake(channel.Id),
                ["guild_id"] = FakeUser.Snowflake(this.Guild.Id)
            }
        });
    }

    private Task<FakeReply> LearnAsync(string name, string content, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand("learn",
            [SlashOption.String("name", name), SlashOption.String("content", content), .. options]));

    private bool IsGeneralPost(RecordedRequest request)
        => request.Method == HttpMethod.Post && request.Path == $"channels/{this.Guild.General.Id}/messages";

    private async Task<JsonNode> WaitForPostAsync()
        => (await this._bot.Rest.WaitForRequestAsync(this.IsGeneralPost)).Json!;

    /// <summary>Waits out the window in case a second post comes, and returns how many there were.</summary>
    private Task<int> GeneralPostCountAfterWindowAsync()
        => GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this._bot.Rest.Requests.Count(this.IsGeneralPost)),
            count => count > 1, NothingHappensWindow);

    private Task<bool> PostsWithinWindowAsync()
        => this._bot.Rest.ReceivesRequestWithinAsync(this.IsGeneralPost, NothingHappensWindow);

    private Task<List<(ulong, string)>> WaitForUsesAsync(int count)
        => GrimoireTestBot.EventuallyAsync(this.UsesAsync, uses => uses.Count >= count);

    private async Task<List<(ulong, string)>> UsesAsync()
    {
        await using var dbContext = database.CreateDbContext();
        return [.. (await dbContext.CustomCommandUsages.ToListAsync(Token)).Select(use => (use.UserId.Value, use.Name.Value))];
    }
}

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
///     <c>/Command</c> cases past what <see cref="CustomCommandUseTests" /> covers (backlog A-48, area 07 section G),
///     and the ping behaviour decided in review C-3.
/// </summary>
[Collection("Test collection")]
public sealed class CustomCommandSlashGapTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database, secondGuild: true);
        await this._bot.ConfigureModerationAsync();
        await this._bot.SetModuleAsync(Module.Commands, true);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>CC-87: an embed command with no colour sends an embed with none, so Discord's default shows.</summary>
    [Fact]
    public async Task AnEmbedCommand_WithoutAColour_SendsNoColour()
    {
        await this.LearnAsync("rules", "Be nice", SlashOption.Boolean("embed", true));

        var reply = await this.CallAsync(this.Guild.User1, "rules");

        reply.Description.ShouldBe("Be nice");
        reply.Embed!["color"].ShouldBeNull();
        await this.WaitForUsesAsync(1);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>CC-89: a role as the mention shows the role's mention.</summary>
    [Fact]
    public async Task ARoleAsTheMention_ShowsTheRole()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");

        var reply = await this.CallAsync(this.Guild.User1, "hug", SlashOption.MentionableRole("mention", this.Guild.ModeratorRoleId));

        reply.Message["content"]!.GetValue<string>().ShouldBe($"<@&{this.Guild.ModeratorRoleId}> gets a hug: ");
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-89: the @everyone role as the mention never becomes a mass ping.</summary>
    [Fact]
    public async Task TheEveryoneRoleAsTheMention_DoesNotPingEveryone()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");

        var reply = await this.CallAsync(this.Guild.User1, "hug", SlashOption.MentionableRole("mention", this.Guild.Id));

        var content = reply.Message["content"]!.GetValue<string>();
        content.ShouldNotContain($"<@&{this.Guild.Id}>");
        content.ShouldNotContain("@everyone");
        (reply.Message["allowed_mentions"]?["parse"]?.AsArray().Select(kind => kind!.GetValue<string>()) ?? [])
            .ShouldNotContain("everyone");
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-90: without a mention or message, both placeholders are removed.</summary>
    [Fact]
    public async Task WithoutMentionOrMessage_ThePlaceholdersAreRemoved()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");

        (await this.CallAsync(this.Guild.User1, "hug")).Message["content"]!.GetValue<string>().ShouldBe(" gets a hug: ");
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-21 (slash path): <c>@everyone</c> in the content shows, and pings nobody.</summary>
    [Fact]
    public async Task EveryoneInTheContent_PingsNobody()
    {
        await this.LearnAsync("look", "@everyone look");

        var reply = await this.CallAsync(this.Guild.User1, "look");

        reply.Message["content"]!.GetValue<string>().ShouldBe("@everyone look");
        ShouldPingNobody(reply.Message["allowed_mentions"]);
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-88, as decided in review C-3: the member given as the mention is pinged, and nobody else.</summary>
    [Fact(Skip = "Fails: no allowed_mentions are set, so DSharpPlus sends parse: [] and the mentioned member isn't pinged. review C-3")]
    public async Task TheMentionedMember_IsPinged()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");

        var reply = await this.CallAsync(this.Guild.User1, "hug", SlashOption.MentionableUser("mention", this.Guild.Moderator),
            SlashOption.String("message", $"and {this.Guild.Admin.Mention}"));

        var allowed = reply.Message["allowed_mentions"]!;
        allowed["users"]!.AsArray().Select(user => user!.GetValue<string>()).ShouldBe([FakeUser.Snowflake(this.Guild.Moderator.Id)]);
        (allowed["parse"]?.AsArray() ?? []).ShouldBeEmpty();
        await this.WaitForUsesAsync(1);
    }

    /// <summary>
    ///     CC-91, as decided in review C-3: the message is left as typed, since the sanitiser goes, and a role or
    ///     <c>@here</c> in it pings nobody.
    /// </summary>
    [Fact(Skip = "Fails: the sanitiser still rewrites @here to \"@ here\". review C-3")]
    public async Task MentionsInTheMessage_AreLeftAsTyped_AndPingNobody()
    {
        await this.LearnAsync("hug", "%Mention gets a hug: %Message");
        var typed = $"<@&{this.Guild.ModeratorRoleId}> and @here";

        var reply = await this.CallAsync(this.Guild.User1, "hug", SlashOption.String("message", typed));

        reply.Message["content"]!.GetValue<string>().ShouldBe($" gets a hug: {typed}");
        ShouldPingNobody(reply.Message["allowed_mentions"]);
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-95: a reply over Discord's 2000 characters is cut, ending in an ellipsis.</summary>
    [Fact]
    public async Task ALongReply_IsCutTo2000Characters()
    {
        await this.LearnAsync("long", new string('x', 1980) + "%Message");

        var reply = await this.CallAsync(this.Guild.User1, "long", SlashOption.String("message", new string('y', 100)));

        var content = reply.Message["content"]!.GetValue<string>();
        content.Length.ShouldBe(2000);
        content.ShouldEndWith("y…");
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-96: a name with a space fails the name converter, so DSharpPlus reports a parse error.</summary>
    [Fact]
    public async Task ANameWithASpace_IsAParseError()
    {
        var reply = await this.CallAsync(this.Guild.User1, "two words");

        reply.Description.ShouldBe("Failed to parse command_name.");
        (await this.UsesAsync()).ShouldBeEmpty();
    }

    /// <summary>CC-97: with the Commands module off, /Command says so and records nothing.</summary>
    [Fact]
    public async Task WithTheModuleDisabled_SaysSo()
    {
        await this.LearnAsync("hello", "Hello there!");
        await this._bot.SetModuleAsync(Module.Commands, false);

        (await this.CallAsync(this.Guild.User1, "hello")).Description.ShouldBe("This module is disabled in this server.");
        (await this.UsesAsync()).ShouldBeEmpty();
    }

    /// <summary>CC-98: an interaction reply works in a channel the bot can't view or post in.</summary>
    [Fact]
    public async Task InAChannelTheBotCannotPostIn_TheReplyStillWorks()
    {
        await this.LearnAsync("hello", "Hello there!");

        var reply = await this._bot.RunSlashCommandAndWaitAsync(this.Guild.Private, "command", this.Guild.Admin,
            SlashOption.String("command_name", "hello"));

        reply.Message["content"]!.GetValue<string>().ShouldBe("Hello there!");
        await this.WaitForUsesAsync(1);
    }

    /// <summary>CC-99: a command learned only in another guild isn't found here.</summary>
    [Fact]
    public async Task ACommandFromAnotherGuild_IsNotFound()
    {
        var second = this._bot.SecondGuild!;
        await this._bot.SetModuleAsync(Module.Commands, true, second);
        await this._bot.RunSlashCommandAndWaitAsync(second.General, "commands", second.Admin, SlashOption.Subcommand("learn",
            SlashOption.String("name", "hello"), SlashOption.String("content", "from B")));

        var interaction = this._bot.RunSlashCommand("command", this.Guild.User1, SlashOption.String("command_name", "hello"));

        await this._bot.Rest.WaitForRequestAsync(interaction.IsOriginalResponseDelete);
        this._bot.Rest.Requests.Count(interaction.IsOriginalResponseEdit).ShouldBe(0);
        (await this.UsesAsync()).ShouldBeEmpty();
    }

    private static void ShouldPingNobody(JsonNode? allowedMentions)
    {
        allowedMentions.ShouldNotBeNull();
        (allowedMentions["parse"]?.AsArray() ?? []).ShouldBeEmpty();
        (allowedMentions["users"]?.AsArray() ?? []).ShouldBeEmpty();
        (allowedMentions["roles"]?.AsArray() ?? []).ShouldBeEmpty();
    }

    private Task<FakeReply> CallAsync(FakeUser invoker, string name, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("command", invoker, [SlashOption.String("command_name", name), .. options]);

    private Task<FakeReply> LearnAsync(string name, string content, params SlashOption[] options)
        => this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand("learn",
            [SlashOption.String("name", name), SlashOption.String("content", content), .. options]));

    private Task<List<(ulong, string)>> WaitForUsesAsync(int count)
        => GrimoireTestBot.EventuallyAsync(this.UsesAsync, uses => uses.Count >= count);

    private async Task<List<(ulong, string)>> UsesAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return [.. (await dbContext.CustomCommandUsages.ToListAsync(Token)).Select(use => (use.UserId.Value, use.Name.Value))];
    }
}

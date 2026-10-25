// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json.Nodes;
using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Moderation;

/// <summary>
///     End-to-end <c>/Warn</c>, case MOD-57 in <c>docs/testing/02-moderation-actions.md</c>: a moderator warns a
///     member and the bot replies, records the sin, posts to the mod log and DMs the member.
/// </summary>
[Collection("Test collection")]
public sealed class WarnTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingElseWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private FakeGuild Guild => this._bot.Guild;

    [Fact]
    public async Task Warn_DefersThenRepliesWithWarnEmbedAndRecordsTheSin()
    {
        var interaction = this._bot.RunSlashCommand("Warn", this.Guild.Moderator,
            SlashOption.User("user", this.Guild.User1),
            SlashOption.String("reason", "be nice"));

        var callback = await this._bot.Rest.WaitForRequestAsync(interaction.IsCallback);
        callback.Json!["type"]!.GetValue<int>().ShouldBe(5, "Warn defers before touching the database");

        var reply = await this._bot.Rest.WaitForRequestAsync(interaction.IsOriginalResponseEdit);
        var embed = reply.Json!["embeds"]![0]!;
        embed["author"]!["name"]!.GetValue<string>().ShouldBe("Warn");
        Field(embed, "User").ShouldBe(this.Guild.User1.Mention);
        Field(embed, "Moderator").ShouldBe(this.Guild.Moderator.MemberMention);
        Field(embed, "Reason").ShouldBe("be nice");

        await using var dbContext = this._database.CreateDbContext();
        var sin = await dbContext.Sins
            .Include(s => s.ReasonHistory)
            .SingleAsync(TestContext.Current.CancellationToken);
        sin.SinType.ShouldBe(SinType.Warn);
        sin.UserId.ShouldBe(new UserId(this.Guild.User1.Id));
        sin.GuildId.ShouldBe(new GuildId(this.Guild.Id));
        sin.Actor.ShouldBe(new ModerationActor.Moderator(new ModeratorId(this.Guild.Moderator.Id)));
        sin.ReasonHistory.ShouldHaveSingleItem().Reason.Value.ShouldBe("be nice");
        Field(embed, "Sin Id").ShouldBe($"**{sin.Id.Value.ToString(CultureInfo.InvariantCulture)}**");

        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Warn_RefusesToWarnTheModeratorThemselves()
    {
        var interaction = this._bot.RunSlashCommand("Warn", this.Guild.Moderator,
            SlashOption.User("user", this.Guild.Moderator),
            SlashOption.String("reason", "testing"));

        var reply = await this._bot.Rest.WaitForRequestAsync(interaction.IsOriginalResponseEdit);
        reply.Json!["embeds"]![0]!["description"]!.GetValue<string>().ShouldBe("You cannot warn yourself.");

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>
    ///     For slash commands DSharpPlus only checks the bot's own permissions; the ManageMessages gate on the
    ///     user is enforced by Discord from the registered default member permissions. This pins that registration.
    /// </summary>
    [Fact]
    public void Warn_IsRegisteredSoOnlyManageMessagesCanSeeIt()
    {
        var registered = this._bot.Rest.RegisteredCommands["warn"];

        registered["default_member_permissions"]!.ToString()
            .ShouldBe(FakeGuild.ManageMessagesPermission.ToString(CultureInfo.InvariantCulture));
        registered["options"]!.AsArray().Select(option => option!["name"]!.GetValue<string>())
            .ShouldBe(["user", "reason"]);
    }

    [Fact]
    public async Task Warn_PostsTheWarnEmbedToTheModLog()
    {
        this._bot.RunSlashCommand("Warn", this.Guild.Moderator,
            SlashOption.User("user", this.Guild.User1),
            SlashOption.String("reason", "be nice"));

        var modLogPost = await this._bot.Rest.WaitForRequestAsync(request =>
            request.Method == HttpMethod.Post && request.Path == $"channels/{this.Guild.ModLog.Id}/messages");

        var embed = modLogPost.Json!["embeds"]![0]!;
        embed["author"]!["name"]!.GetValue<string>().ShouldBe("Warn");
        Field(embed, "Reason").ShouldBe("be nice");
    }

    [Fact(Skip = "Fails: slash commands resolve the user option to a DiscordUser, never a DiscordMember, " +
                 "so Warn's `user is DiscordMember` check is always false and no DM is sent.")]
    public async Task Warn_DirectMessagesTheWarnedMember()
    {
        var interaction = this._bot.RunSlashCommand("Warn", this.Guild.Moderator,
            SlashOption.User("user", this.Guild.User1),
            SlashOption.String("reason", "be nice"));
        await this._bot.Rest.WaitForRequestAsync(interaction.IsOriginalResponseEdit);

        var dmChannel = await this._bot.Rest.WaitForRequestAsync(request =>
            request.Method == HttpMethod.Post && request.Path == "users/@me/channels");
        dmChannel.Json!["recipient_id"]!.ToString().ShouldBe(FakeUser.Snowflake(this.Guild.User1.Id));

        var dm = await this._bot.Rest.WaitForRequestAsync(request =>
            request.Method == HttpMethod.Post && request.Path.StartsWith("channels/", StringComparison.Ordinal)
                                              && request.Path != $"channels/{this.Guild.ModLog.Id}/messages");
        dm.Json!["embeds"]![0]!["description"]!.GetValue<string>()
            .ShouldBe($"You have been warned by {this.Guild.Moderator.MemberMention} for be nice");

        (await this._bot.Rest.ReceivesRequestWithinAsync(request =>
                request.Path == $"channels/{this.Guild.ModLog.Id}/messages"
                && request.Json?["embeds"]?[0]?["description"]?.ToString().StartsWith("Was not able", StringComparison.Ordinal) == true,
            NothingElseWindow))
            .ShouldBeFalse("the DM succeeded, so no failure notice belongs in the mod log");
    }

    /// <summary>MOD-58: when the DM fails, the mod log gets a red line as well as the warn embed.</summary>
    [Fact(Skip = "Blocked: Warn never attempts the DM, because the user option is never a DiscordMember. KI-19")]
    public async Task Warn_WhenTheMemberHasDmsClosed_TellsTheModLog()
    {
        this._bot.Rest.On(HttpMethod.Post, @"channels/\d+/messages", request =>
            request.Path == $"channels/{this.Guild.ModLog.Id}/messages"
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(new JsonObject
                    {
                        ["id"] = "1",
                        ["channel_id"] = FakeUser.Snowflake(this.Guild.ModLog.Id),
                        ["content"] = "",
                        ["author"] = this.Guild.Bot.ToJson(),
                        ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
                        ["embeds"] = new JsonArray(),
                        ["attachments"] = new JsonArray(),
                        ["mentions"] = new JsonArray(),
                        ["mention_roles"] = new JsonArray(),
                        ["type"] = 0
                    }.ToJsonString())
                }
                : FakeDiscordRest.Error(System.Net.HttpStatusCode.Forbidden, 50007, "Cannot send messages to this user"));

        this._bot.RunSlashCommand("Warn", this.Guild.Moderator,
            SlashOption.User("user", this.Guild.User1), SlashOption.String("reason", "x"));

        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()
            == $"Was not able to send a direct message with the warn details to {this.Guild.User1.Mention}");
    }

    /// <summary>MOD-59: a user who isn't in the server is still warned, with no DM attempted.</summary>
    [Fact]
    public async Task Warn_ForAUserNotInTheServer_RecordsTheSinWithoutADm()
    {
        var stranger = new FakeUser(1_100_000_000_000_000_097, "stranger");

        var reply = await this._bot.RunSlashCommandAndWaitAsync("Warn", this.Guild.Moderator,
            SlashOption.User("user", stranger), SlashOption.String("reason", "x"));

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Warn");
        await this._bot.WaitForModLogEmbedAsync(embed => embed["author"]?["name"]?.GetValue<string>() == "Warn");
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.SingleAsync(TestContext.Current.CancellationToken)).UserId.ShouldBe(new UserId(stranger.Id));
        this._bot.Rest.Requests.Count(request => request.Path == "users/@me/channels").ShouldBe(0);
    }

    /// <summary>MOD-61 and MOD-62: Discord's client enforces the reason's length and presence.</summary>
    [Fact]
    public void Warn_ReasonIsRegisteredAsRequired_AndAtMostOneThousandCharacters()
    {
        var reason = this._bot.Rest.RegisteredCommands["warn"]["options"]!.AsArray()
            .Single(option => option!["name"]!.GetValue<string>() == "reason")!;

        reason["required"]!.GetValue<bool>().ShouldBeTrue();
        reason["max_length"]!.GetValue<int>().ShouldBe(1000);
    }

    /// <summary>MOD-65 and MOD-66: anyone Discord lets through can warn anyone, with no hierarchy or bot checks.</summary>
    [Theory]
    [InlineData("owner", "user1")]
    [InlineData("admin", "user1")]
    [InlineData("mod", "admin")]
    [InlineData("mod", "bot")]
    public async Task Warn_HasNoHierarchyOrBotChecks(string moderatorName, string targetName)
    {
        var moderator = this.UserNamed(moderatorName);
        var target = this.UserNamed(targetName);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("Warn", moderator,
            SlashOption.User("user", target), SlashOption.String("reason", "x"));

        reply.Embed!["author"]!["name"]!.GetValue<string>().ShouldBe("Warn");
        await using var dbContext = this._database.CreateDbContext();
        var sin = await dbContext.Sins.SingleAsync(TestContext.Current.CancellationToken);
        sin.UserId.ShouldBe(new UserId(target.Id));
        sin.Actor.ShouldBe(new ModerationActor.Moderator(new ModeratorId(moderator.Id)));
    }

    /// <summary>MOD-67.</summary>
    [Fact]
    public async Task Warn_WithModerationDisabled_IsRefused_AndRecordsNothing()
    {
        await this._bot.SetModuleAsync(Grimoire.Settings.Enums.Module.Moderation, false);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("Warn", this.Guild.Moderator,
            SlashOption.User("user", this.Guild.User1), SlashOption.String("reason", "x"));

        reply.Description.ShouldBe("This module is disabled in this server.");
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>MOD-68: the reason is stored and shown verbatim, and only in the embed, which never pings.</summary>
    [Fact]
    public async Task Warn_KeepsMarkupInTheReason_AndPingsNobody()
    {
        const string reason = "@everyone **hi** `code`";

        var reply = await this._bot.RunSlashCommandAndWaitAsync("Warn", this.Guild.Moderator,
            SlashOption.User("user", this.Guild.User1), SlashOption.String("reason", reason));

        Field(reply.Embed!, "Reason").ShouldBe(reason);
        reply.Message["content"]?.GetValue<string>().ShouldBeNullOrEmpty();
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.Include(sin => sin.ReasonHistory).SingleAsync(TestContext.Current.CancellationToken))
            .ReasonHistory.ShouldHaveSingleItem().Reason.Value.ShouldBe(reason);
    }

    private FakeUser UserNamed(string name) => name switch
    {
        "owner" => this.Guild.Owner,
        "admin" => this.Guild.Admin,
        "mod" => this.Guild.Moderator,
        "user1" => this.Guild.User1,
        "bot" => this.Guild.Bot,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
    };

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray()
            .Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

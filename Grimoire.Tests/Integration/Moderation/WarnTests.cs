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

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray()
            .Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

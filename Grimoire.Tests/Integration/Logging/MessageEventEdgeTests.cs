// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json.Nodes;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>
///     Edge cases of saving, editing, deleting, bulk deleting and reacting to messages (backlog A-44, area 05
///     sections E to I and L). The three message logs point at <c>#modlog</c>; posts are told apart by their titles.
/// </summary>
[Collection("Test collection")]
public sealed class MessageEventEdgeTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);
    private static readonly GuildLogType[] MessageLogTypes =
        [GuildLogType.MessageEdited, GuildLogType.MessageDeleted, GuildLogType.BulkMessageDeleted];

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private FakeChannel Channel => this._bot.Guild.General;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this.ConfigureAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task ConfigureAsync()
    {
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureMessageLogAsync();
        foreach (var logType in MessageLogTypes)
            await this.PointLogAtAsync(logType, this.Guild.ModLog.Id);
    }

    // ── E. Saving ─────────────────────────────────────────────────────────────

    /// <summary>LOG-60: a message is saved with its content even when no log channel is set.</summary>
    [Fact]
    public async Task AMessage_IsSaved_EvenWithNoLogChannelSet()
    {
        foreach (var logType in MessageLogTypes)
            await this.PointLogAtAsync(logType, null);

        var messageId = await this.SendLoggedAsync("hello");

        await using var dbContext = this._database.CreateDbContext();
        var saved = await dbContext.Messages.AsNoTracking().SingleAsync(message => message.Id == new MessageId(messageId),
            TestContext.Current.CancellationToken);
        saved.UserId.ShouldBe(new UserId(this.Guild.User1.Id));
        saved.ChannelId.ShouldBe(new ChannelId(this.Channel.Id));
        (await this.HistoryOfAsync<MessageCreatedEntry>(messageId)).ShouldHaveSingleItem().Content.ToString().ShouldBe("hello");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LOG-61: each attachment is saved by its full signed CDN URL.</summary>
    [Fact]
    public async Task EachAttachment_IsSavedWithItsFullUrl()
    {
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Channel, "two files", "a.png", "b.txt");

        var urls = await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Attachments.Where(attachment => attachment.MessageId == new MessageId(messageId))
                .Select(attachment => attachment.AttachmentUrl.Value).ToListAsync();
        }, found => found.Count == 2);

        urls.Count.ShouldBe(2);
        urls.ShouldContain(url => url.StartsWith($"https://cdn.discordapp.com/attachments/{this.Channel.Id}/") && url.Contains("/a.png?ex="));
        urls.ShouldContain(url => url.StartsWith($"https://cdn.discordapp.com/attachments/{this.Channel.Id}/") && url.Contains("/b.txt?ex="));
    }

    /// <summary>LOG-62: a reply is saved with the message it replies to.</summary>
    [Fact]
    public async Task AReply_IsSavedWithTheMessageItRepliesTo()
    {
        var original = await this.SendLoggedAsync("question");

        var reply = await this.SendReplyAsync("answer", original);

        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Messages.AsNoTracking().SingleAsync(message => message.Id == new MessageId(reply),
            TestContext.Current.CancellationToken)).ReferencedMessageId.ShouldBe(new MessageId(original));
    }

    // ── F. Edits ──────────────────────────────────────────────────────────────

    /// <summary>LOG-71: a second edit is compared with the first edit, not with the original.</summary>
    [Fact]
    public async Task ASecondEdit_ShowsTheFirstEditAsBefore()
    {
        var messageId = await this.SendLoggedAsync("before");
        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "after");
        await GrimoireTestBot.EventuallyAsync(() => this.HistoryOfAsync<MessageEditedEntry>(messageId), rows => rows.Count == 1);

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "third");

        var embed = await this.WaitForLogAsync($"Message edited in #{this.Channel.Name}", ("After", "third"));
        Field(embed, "Before").ShouldBe("after");
        (await GrimoireTestBot.EventuallyAsync(() => this.HistoryOfAsync<MessageEditedEntry>(messageId), rows => rows.Count == 2))
            .Select(entry => entry.Content.ToString()).Order().ShouldBe(["after", "third"]);
    }

    /// <summary>LOG-76: an edit that removes all the text of a post with an image is still an edit.</summary>
    [Fact(Skip = "Fails: an edit with empty content returns before the stored text is compared, so it isn't recorded or logged. KI-69")]
    public async Task AnEditThatRemovesAllTheText_IsLogged()
    {
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Channel, "caption", "cat.png");
        await this.WaitUntilSavedAsync(messageId);

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "");

        var embed = await this.WaitForLogAsync($"Message edited in #{this.Channel.Name}");
        Field(embed, "Before").ShouldBe("caption");
        Field(embed, "After").ShouldBe("`blank`");
        (await this.HistoryOfAsync<MessageEditedEntry>(messageId)).ShouldHaveSingleItem();
    }

    /// <summary>LOG-77: Discord adding a link preview sends an update with the same text and no edit time.</summary>
    [Fact]
    public async Task ALinkPreviewBeingAdded_IsNotLogged()
    {
        const string link = "look https://example.com";
        var messageId = await this.SendLoggedAsync(link);
        var update = this.MessageJson(messageId, this.Guild.User1, link);
        update["embeds"] = new JsonArray(new JsonObject { ["type"] = "link", ["url"] = "https://example.com", ["title"] = "Example" });

        this._bot.Gateway.Dispatch("MESSAGE_UPDATE", update);

        (await this.LoggedWithinAsync("Message edited", NothingHappensWindow)).ShouldBeFalse();
        (await this.HistoryOfAsync<MessageEditedEntry>(messageId)).ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LOG-78: with the edit log off nothing is posted, but the edit is still recorded.</summary>
    [Fact]
    public async Task WithTheEditLogOff_TheEditIsRecorded_ButNotPosted()
    {
        await this.PointLogAtAsync(GuildLogType.MessageEdited, null);
        var messageId = await this.SendLoggedAsync("before");

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "after");

        (await GrimoireTestBot.EventuallyAsync(() => this.HistoryOfAsync<MessageEditedEntry>(messageId), rows => rows.Count == 1))
            .ShouldHaveSingleItem().Content.ToString().ShouldBe("after");
        (await this.LoggedWithinAsync("Message edited", NothingHappensWindow)).ShouldBeFalse();
    }

    // ── G. Deletes ────────────────────────────────────────────────────────────

    /// <summary>LOG-90: a deleted reply links the message it replied to.</summary>
    [Fact]
    public async Task ADeletedReply_LinksTheMessageItRepliedTo()
    {
        var original = await this.SendLoggedAsync("question");
        var reply = await this.SendReplyAsync("answer", original);

        this._bot.DeleteMessage(reply, this.Channel);

        var embed = await this.WaitForLogAsync($"Message deleted in #{this.Channel.Name}", ("**Content**", "answer"));
        embed["description"]!.GetValue<string>()
            .ShouldBe($"**[Reply To](https://discordapp.com/channels/{this.Guild.Id}/{this.Channel.Id}/{original})**");
        (await this.HistoryOfAsync<MessageDeletedEntry>(reply)).ShouldHaveSingleItem();
    }

    /// <summary>LOG-100: a delete log the bot can't post in is reported, and the delete is still recorded.</summary>
    [Fact]
    public async Task ADeleteLogTheBotCannotPostIn_IsReported()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this.ConfigureAsync();
        await this.PointLogAtAsync(GuildLogType.MessageDeleted, this.Guild.Private.Id);
        var messageId = await this.SendLoggedAsync("gone");

        this._bot.DeleteMessage(messageId, this.Channel);

        await this.WaitForCannotPostReportAsync(this.Guild.Private);
        (await this.HistoryOfAsync<MessageDeletedEntry>(messageId)).ShouldHaveSingleItem();
    }

    // ── H. Bulk deletes ───────────────────────────────────────────────────────

    /// <summary>LOG-111: the count should be every message purged, not only those the log saved.</summary>
    [Fact(Skip = "Fails: \"Message Count\" counts only the saved messages, so a purge of 5 with 2 saved reports 2. KI-70")]
    public async Task BulkDelete_CountsEveryMessagePurged()
    {
        ulong[] ids =
        [
            this._bot.NextSnowflake(), this._bot.NextSnowflake(), this._bot.NextSnowflake(),
            await this.SendLoggedAsync("one"), await this.SendLoggedAsync("two")
        ];

        this._bot.BulkDeleteMessages(this.Channel, ids);

        var post = await this.WaitForBulkLogAsync();
        post.Json!["embeds"]![0]!["description"]!.GetValue<string>().ShouldStartWith("**Message Count:** 5\n");
    }

    /// <summary>LOG-112: a purge of messages the log never saved posts nothing.</summary>
    [Fact]
    public async Task BulkDelete_OfOnlyUnsavedMessages_PostsNothing()
    {
        this._bot.BulkDeleteMessages(this.Channel, this._bot.NextSnowflake(), this._bot.NextSnowflake());

        (await this.BulkLoggedWithinAsync(NothingHappensWindow)).ShouldBeFalse();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LOG-113: the dump lists each attachment's URL.</summary>
    [Fact]
    public async Task BulkDelete_DumpListsTheAttachments()
    {
        var withFile = this._bot.SendMessage(this.Guild.User1, this.Channel, "see file", "cat.png");
        await this.WaitUntilSavedAsync(withFile);
        var plain = await this.SendLoggedAsync("plain");

        this._bot.BulkDeleteMessages(this.Channel, withFile, plain);

        var dump = (await this.WaitForBulkLogAsync()).Files.ShouldHaveSingleItem().Value;
        dump.ShouldContain($"Id: {withFile}\nContent: see file\nAttachments: https://cdn.discordapp.com/attachments/{this.Channel.Id}/");
        dump.ShouldContain("/cat.png?ex=");
        dump[dump.IndexOf($"Id: {plain}", StringComparison.Ordinal)..].ShouldNotContain("Attachments:");
    }

    /// <summary>LOG-114: with the bulk delete log off nothing is posted, but each delete is recorded.</summary>
    [Fact]
    public async Task WithTheBulkDeleteLogOff_TheDeletesAreRecorded_ButNotPosted()
    {
        await this.PointLogAtAsync(GuildLogType.BulkMessageDeleted, null);
        ulong[] ids = [await this.SendLoggedAsync("one"), await this.SendLoggedAsync("two")];

        this._bot.BulkDeleteMessages(this.Channel, ids);

        foreach (var id in ids)
            (await GrimoireTestBot.EventuallyAsync(() => this.HistoryOfAsync<MessageDeletedEntry>(id), rows => rows.Count == 1))
                .ShouldHaveSingleItem();
        (await this.BulkLoggedWithinAsync(NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>
    ///     LOG-115: Discord refuses the dump (here a 403 on the log channel, standing in for a missing Attach Files),
    ///     so the guild hears it; the deletes are still recorded.
    /// </summary>
    [Fact]
    public async Task ABulkDeleteLogThatDiscordRefuses_IsReported()
    {
        await this.PointLogAtAsync(GuildLogType.BulkMessageDeleted, this.Channel.Id);
        this._bot.Rest.On(HttpMethod.Post, $"channels/{this.Channel.Id}/messages",
            _ => FakeDiscordRest.Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions"));
        ulong[] ids = [await this.SendLoggedAsync("one"), await this.SendLoggedAsync("two")];

        this._bot.BulkDeleteMessages(this.Channel, ids);

        await this.WaitForCannotPostReportAsync(this.Channel);
        foreach (var id in ids)
            (await this.HistoryOfAsync<MessageDeletedEntry>(id)).ShouldHaveSingleItem();
    }

    /// <summary>LOG-116: with the message log off, a purge records and posts nothing.</summary>
    [Fact]
    public async Task WithTheMessageLogOff_ABulkDeleteIsIgnored()
    {
        ulong[] ids = [await this.SendLoggedAsync("one"), await this.SendLoggedAsync("two")];
        await this._bot.SetModuleAsync(Module.MessageLog, false);

        this._bot.BulkDeleteMessages(this.Channel, ids);

        (await this.BulkLoggedWithinAsync(NothingHappensWindow)).ShouldBeFalse();
        foreach (var id in ids)
            (await this.HistoryOfAsync<MessageDeletedEntry>(id)).ShouldBeEmpty();
    }

    // ── I. Reactions ──────────────────────────────────────────────────────────

    /// <summary>LOG-121: a unicode emoji next to a custom one is a second row.</summary>
    [Fact]
    public async Task AUnicodeReaction_NextToACustomOne_IsASecondRow()
    {
        var messageId = await this.SendLoggedAsync("react to this");
        this._bot.AddReaction(this.Guild.User1, this.Channel, messageId, new FakeEmoji.Custom(1_100_000_000_000_000_301, "party"));
        await this.WaitForReactionsAsync(messageId, rows => rows.Count == 1);

        this._bot.AddReaction(this.Guild.User1, this.Channel, messageId, new FakeEmoji.Standard("👍"));

        var rows = await this.WaitForReactionsAsync(messageId, found => found.Count == 2);
        rows.Select(reaction => reaction.Emoji.ToString()).Order().ShouldBe(["<:party:1100000000000000301>", "👍"]);
        rows.ShouldAllBe(reaction => reaction.RemovedAt == null);
    }

    /// <summary>LOG-128: after a restart the message isn't cached, but clearing its reactions still marks them removed.</summary>
    [Fact]
    public async Task ClearingReactions_OnAMessageFromBeforeARestart_SoftDeletesThem()
    {
        var messageId = await this.SendLoggedAsync("react to this");
        this._bot.AddReaction(this.Guild.User1, this.Channel, messageId, new FakeEmoji.Standard("👍"));
        this._bot.AddReaction(this.Guild.Moderator, this.Channel, messageId, new FakeEmoji.Standard("🎉"));
        (await this.WaitForReactionsAsync(messageId, rows => rows.Count == 2)).Count.ShouldBe(2);
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database);

        this._bot.RemoveAllReactions(this.Channel, messageId);

        var rows = await this.WaitForReactionsAsync(messageId, found => found.All(reaction => reaction.RemovedAt != null));
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(reaction => reaction.RemovedAt != null);
    }

    // ── L. What the cleanup purges ────────────────────────────────────────────

    /// <summary>LOG-187: only message-log posts are queued for purge; join and leave logs are kept.</summary>
    [Fact]
    public async Task OnlyMessageLogPosts_AreQueuedForPurge()
    {
        await this._bot.SetModuleAsync(Module.UserLog, true);
        foreach (var logType in new[] { GuildLogType.UserJoined, GuildLogType.UserLeft })
            await this.PointLogAtAsync(logType, this.Guild.ModLog.Id);
        this._bot.MemberLeaves(this.Guild.User1);
        await this.WaitForLogAsync("User Left");
        this._bot.MemberJoins(this.Guild.User1);
        await this.WaitForLogAsync("User Joined");
        var messageId = await this.SendLoggedAsync("before");
        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "after");
        await this.WaitForLogAsync($"Message edited in #{this.Channel.Name}");

        await GrimoireTestBot.EventuallyAsync(this.QueuedForPurgeAsync, rows => rows.Count > 0);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        var queued = await this.QueuedForPurgeAsync();

        var editPost = this._bot.Rest.PostsIn(this.Guild.ModLog).Single(post =>
            post["embeds"]?[0]?["author"]?["name"]?.GetValue<string>() == $"Message edited in #{this.Channel.Name}");
        queued.ShouldBe([ulong.Parse(editPost["id"]!.GetValue<string>())]);
    }

    /// <summary>LOG-188: the bot's own log posts aren't saved, so purging them logs nothing.</summary>
    [Fact]
    public async Task PurgingTheBotsLogPost_LogsNothing()
    {
        var messageId = await this.SendLoggedAsync("gone");
        this._bot.DeleteMessage(messageId, this.Channel);
        await this.WaitForLogAsync($"Message deleted in #{this.Channel.Name}");
        var logPost = ulong.Parse(this._bot.Rest.PostsIn(this.Guild.ModLog).Last()["id"]!.GetValue<string>());
        var postsBefore = this._bot.Rest.Requests.Count(this._bot.IsModLogPost);

        this._bot.BulkDeleteMessages(this.Guild.ModLog, logPost);
        this._bot.DeleteMessage(logPost, this.Guild.ModLog);

        await Task.Delay(NothingHappensWindow, TestContext.Current.CancellationToken);
        this._bot.Rest.Requests.Count(this._bot.IsModLogPost).ShouldBe(postsBefore);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.MessageHistory.AnyAsync(entry => entry.MessageId == new MessageId(logPost), TestContext.Current.CancellationToken))
            .ShouldBeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task PointLogAtAsync(GuildLogType logType, ulong? channelId)
        => (await this.Settings.SetLogChannelSetting(logType, new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id),
            channelId is { } id ? new ChannelId(id) : null)).ShouldBeSetOrUnchanged();

    private async Task<ulong> SendLoggedAsync(string content)
    {
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Channel, content);
        await this.WaitUntilSavedAsync(messageId);
        return messageId;
    }

    /// <summary><c>user1</c> replies to <paramref name="original" />, as Discord sends it: type 19 with the message referenced.</summary>
    private async Task<ulong> SendReplyAsync(string content, ulong original)
    {
        var messageId = this._bot.NextSnowflake();
        var reply = this.MessageJson(messageId, this.Guild.User1, content);
        reply["type"] = 19;
        reply["message_reference"] = new JsonObject
        {
            ["message_id"] = FakeUser.Snowflake(original),
            ["channel_id"] = FakeUser.Snowflake(this.Channel.Id),
            ["guild_id"] = FakeUser.Snowflake(this.Guild.Id)
        };
        reply["referenced_message"] = this.MessageJson(original, this.Guild.User1, "question");
        this._bot.Gateway.Dispatch("MESSAGE_CREATE", reply);
        await this.WaitUntilSavedAsync(messageId);
        return messageId;
    }

    /// <summary>A message in <c>#general</c> with no edit time, for the events the harness has no helper for.</summary>
    private JsonObject MessageJson(ulong messageId, FakeUser author, string content) => new()
    {
        ["id"] = FakeUser.Snowflake(messageId),
        ["channel_id"] = FakeUser.Snowflake(this.Channel.Id),
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
        ["type"] = 0,
        ["flags"] = 0
    };

    private async Task WaitUntilSavedAsync(ulong messageId)
        => (await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId));
        }, saved => saved)).ShouldBeTrue();

    private Task WaitForCannotPostReportAsync(FakeChannel channel)
        => this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()?.StartsWith(
            $"I could not post in {channel.Mention}.", StringComparison.Ordinal) == true);

    private Task<JsonNode> WaitForLogAsync(string title, (string Name, string Value)? field = null)
        => this._bot.WaitForModLogEmbedAsync(embed => embed["author"]?["name"]?.GetValue<string>() == title
                                                      && (field is not { } wanted
                                                          || embed["fields"]?.AsArray().Any(f => f?["name"]?.GetValue<string>() == wanted.Name
                                                              && f["value"]?.GetValue<string>() == wanted.Value) == true));

    private Task<bool> LoggedWithinAsync(string titleStart, TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                               && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString()
                                                                   .StartsWith(titleStart, StringComparison.Ordinal) == true, window);

    private Task<RecordedRequest> WaitForBulkLogAsync()
        => this._bot.Rest.WaitForRequestAsync(request => this._bot.IsModLogPost(request)
                                                         && request.Json?["embeds"]?[0]?["title"]?.GetValue<string>() == "Bulk Message Delete");

    private Task<bool> BulkLoggedWithinAsync(TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Post
                                                               && request.Json?["embeds"]?[0]?["title"]?.GetValue<string>() == "Bulk Message Delete",
            window);

    private async Task<List<T>> HistoryOfAsync<T>(ulong messageId) where T : MessageHistoryEntry
    {
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.MessageHistory.OfType<T>().Where(entry => entry.MessageId == new MessageId(messageId))
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<ulong>> QueuedForPurgeAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return [.. (await dbContext.OldLogMessages.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken))
            .Select(row => row.Id.Value)];
    }

    private Task<List<Reaction>> WaitForReactionsAsync(ulong messageId, Func<List<Reaction>, bool> isDone)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Reactions.AsNoTracking()
                .Where(reaction => reaction.MessageId == new MessageId(messageId))
                .ToListAsync();
        }, isDone);

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

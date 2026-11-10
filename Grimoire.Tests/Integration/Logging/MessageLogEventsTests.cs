// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Grimoire.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>
///     Message edits, deletes and bulk deletes, and the message-log posts they cause (backlog A-22). The three logs
///     point at <c>#modlog</c>; posts are told apart by their embed titles. Attachments and PluralKit proxies are in
///     A-34.
/// </summary>
[Collection("Test collection")]
public sealed class MessageLogEventsTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private FakeChannel Channel => this._bot.Guild.General;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureMessageLogAsync();
        var settings = this._bot.Services.GetRequiredService<SettingsModule>();
        foreach (var logType in new[] { GuildLogType.MessageEdited, GuildLogType.MessageDeleted, GuildLogType.BulkMessageDeleted })
            (await settings.SetLogChannelSetting(logType, new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id),
                new ChannelId(this.Guild.ModLog.Id))).ShouldBeSetOrUnchanged();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Edits ─────────────────────────────────────────────────────────────────

    /// <summary>An edit is recorded and logged with the content before and after.</summary>
    /// <remarks>Cases: LOG-70.</remarks>
    [Fact]
    public async Task Edit_IsRecorded_AndLoggedWithBeforeAndAfter()
    {
        var messageId = await this.SendLoggedAsync("before");

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "after");

        var embed = await this.WaitForLogAsync($"Message edited in #{this.Channel.Name}");
        Field(embed, "Channel").ShouldBe(this.Channel.Mention);
        Field(embed, "Message Id").ShouldBe(messageId.ToString());
        Field(embed, "Author").ShouldBe(this.Guild.User1.Mention);
        Field(embed, "Before").ShouldBe("before");
        Field(embed, "After").ShouldBe("after");
        (await this.HistoryOfAsync<MessageEditedEntry>(messageId)).ShouldHaveSingleItem().Content.ToString().ShouldBe("after");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>An edit of an unlogged message, and the bot's own edits, are ignored.</summary>
    /// <remarks>Cases: LOG-73 (unlogged), LOG-74 (bot).</remarks>
    [Theory]
    [InlineData("unlogged")]
    [InlineData("bot")]
    public async Task Edits_TheLogDoesNotTrack_AreIgnored(string situation)
    {
        if (situation == "unlogged")
            this._bot.EditMessage(1_100_000_000_000_008_001, this.Guild.User1, this.Channel, "new");
        else
            this._bot.EditMessage(1_100_000_000_000_008_002, this.Guild.Bot, this.Channel, "new");

        (await this.LoggedWithinAsync("Message edited", NothingHappensWindow)).ShouldBeFalse();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>An edit that only changes letter case isn't logged.</summary>
    /// <remarks>Cases: LOG-72.</remarks>
    [Fact]
    public async Task Edit_ThatOnlyChangesCase_IsIgnored()
    {
        var messageId = await this.SendLoggedAsync("Hello There");

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "hello there");

        (await this.LoggedWithinAsync("Message edited", NothingHappensWindow)).ShouldBeFalse();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    // ── Deletes ───────────────────────────────────────────────────────────────

    /// <summary>A delete is recorded and logged with the author and the last known content.</summary>
    /// <remarks>Cases: LOG-85.</remarks>
    [Fact]
    public async Task Delete_IsRecorded_AndLoggedWithTheContent()
    {
        var messageId = await this.SendLoggedAsync("say goodbye");

        this._bot.DeleteMessage(messageId, this.Channel);

        var embed = await this.WaitForLogAsync($"Message deleted in #{this.Channel.Name}");
        Field(embed, "Author").ShouldBe(this.Guild.User1.Mention);
        Field(embed, "**Content**").ShouldBe("say goodbye");
        embed["fields"]!.AsArray().Select(f => f!["name"]!.GetValue<string>()).ShouldNotContain("Deleted By");
        (await this.HistoryOfAsync<MessageDeletedEntry>(messageId)).ShouldHaveSingleItem();
    }

    /// <summary>
    ///     L-1: attachments are stored by their full signed URL, so a deleted image is downloaded from it and re-uploaded with
    ///     the log, and other files are listed by name.
    /// </summary>
    /// <remarks>Cases: LOG-91.</remarks>
    [Fact]
    public async Task Delete_WithAttachments_ReuploadsTheImage_AndListsTheOtherFiles()
    {
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Channel, "look", "cat.png", "notes.txt");
        var stored = await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = database.CreateDbContext();
            return await dbContext.Attachments.Where(attachment => attachment.MessageId == new MessageId(messageId))
                .Select(attachment => attachment.AttachmentUrl.Value)
                .ToListAsync(TestContext.Current.CancellationToken);
        }, urls => urls.Count == 2);
        stored.ShouldAllBe(url => url.StartsWith("https://cdn.discordapp.com/attachments/") && url.Contains("?ex="));

        this._bot.DeleteMessage(messageId, this.Channel);

        var post = await this._bot.Rest.WaitForRequestAsync(request => this._bot.IsModLogPost(request)
                                                                      && request.Json?["embeds"]?[0]?["author"]?["name"]
                                                                          ?.GetValue<string>() == $"Message deleted in #{this.Channel.Name}");
        var embeds = post.Json!["embeds"]!.AsArray();
        embeds.Select(embed => embed!["image"]?["url"]?.GetValue<string>()).OfType<string>()
            .ShouldHaveSingleItem().ShouldStartWith("attachment://");
        embeds.SelectMany(embed => embed!["fields"]!.AsArray())
            .Single(field => field!["name"]!.GetValue<string>() == "Non-Image Attachments")!["value"]!.GetValue<string>()
            .ShouldContain("notes.txt");
        post.Files.ShouldHaveSingleItem();
        this._bot.Cdn.Downloads.ShouldContain(uri => uri.OriginalString == stored.Single(url => url.Contains("cat.png")));
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     I-6: Discord caps a message's embeds at 6,000 characters together. Each image used to carry a full copy of
    ///     the text, so four images with about 1,500 characters lost the whole log. Only the first embed has it now.
    /// </summary>
    /// <remarks>Cases: LOG-93.</remarks>
    [Fact]
    public async Task Delete_OfALongMessageWithFourImages_FitsInOnePost()
    {
        var text = string.Join(' ', Enumerable.Repeat("words", 330));
        var messageId = await this.SendLoggedWithAttachmentsAsync(text, "a.png", "b.png", "c.png", "d.png");

        this._bot.DeleteMessage(messageId, this.Channel);

        var embeds = (await this.WaitForDeleteLogPostAsync()).Json!["embeds"]!.AsArray();
        embeds.Count.ShouldBe(4);
        embeds.Sum(embed => EmbedLength(embed!)).ShouldBeLessThanOrEqualTo(6000);
        embeds.Skip(1).ShouldAllBe(embed => embed!["fields"] == null || embed["fields"]!.AsArray().Count == 0);
        embeds.Select(embed => embed!["url"]!.GetValue<string>()).Distinct().ShouldHaveSingleItem("one gallery");
    }

    /// <summary>I-6: a file over the upload limit would make Discord refuse the whole post, so it's listed instead.</summary>
    /// <remarks>Cases: LOG-92.</remarks>
    [Fact]
    public async Task Delete_WithAnImageTooLargeToUpload_ListsIt_AndStillLogs()
    {
        this._bot.Cdn.ClaimSize("huge.png", DiscordImageEmbedService.MaxFileSize + 1);
        var messageId = await this.SendLoggedWithAttachmentsAsync("look", "small.png", "huge.png");

        this._bot.DeleteMessage(messageId, this.Channel);

        var post = await this.WaitForDeleteLogPostAsync();
        post.Files.ShouldHaveSingleItem().Key.ShouldBe("attachment0.png");
        FieldsOf(post).ShouldContain(("Failed to download these images.", "**huge.png**"));
    }

    /// <summary>
    ///     I-16: an upper-case extension is still an image, listed once and not also as a non-image. The upload name
    ///     has one dot, and the signed query string stays out of the names.
    /// </summary>
    /// <remarks>Cases: LOG-91.</remarks>
    [Fact]
    public async Task Delete_WithAnUpperCaseExtension_TreatsItAsAnImage()
    {
        var messageId = await this.SendLoggedWithAttachmentsAsync("look", "CAT.PNG", "notes.txt");

        this._bot.DeleteMessage(messageId, this.Channel);

        var post = await this.WaitForDeleteLogPostAsync();
        post.Files.ShouldHaveSingleItem().Key.ShouldBe("attachment0.png");
        FieldsOf(post).ShouldContain(("Attachments", "**CAT.PNG**"));
        FieldsOf(post).ShouldContain(("Non-Image Attachments", "**notes.txt**"));
    }

    /// <summary>Rows saved before full URLs were stored hold only a file name. It can't be downloaded, but it's listed.</summary>
    /// <remarks>Cases: LOG-91.</remarks>
    [Fact]
    public async Task Delete_WithAnAttachmentStoredByNameOnly_StillLogs()
    {
        const ulong oldMessage = 1_100_000_000_000_008_030;
        await using (var seed = database.CreateDbContext())
        {
            seed.Messages.Add(new Message
            {
                Id = new MessageId(oldMessage),
                UserId = new UserId(this.Guild.User1.Id),
                GuildId = new GuildId(this.Guild.Id),
                ChannelId = new ChannelId(this.Channel.Id),
                CreatedTimestamp = DateTimeOffset.UtcNow.AddDays(-3),
                Attachments =
                [
                    new Attachment
                    {
                        Id = new AttachmentId(1_100_000_000_000_008_031), MessageId = new MessageId(oldMessage),
                        AttachmentUrl = AttachmentUrl.Create("old.png")
                    }
                ]
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        this._bot.DeleteMessage(oldMessage, this.Channel);

        FieldsOf(await this.WaitForDeleteLogPostAsync()).ShouldContain(("Failed to download these images.", "**old.png**"));
    }

    /// <summary>I-7: Discord's 5xx is retried, and the retry must upload the image again, not an emptied stream.</summary>
    /// <remarks>Cases: LOG-91.</remarks>
    [Fact]
    public async Task Delete_WithAnImage_WhoseFirstPostFails_UploadsItAgainOnTheRetry()
    {
        var messageId = await this.SendLoggedWithAttachmentsAsync("look", "cat.png");

        this._bot.Rest.FailOnce(HttpMethod.Post, $"channels/{this.Guild.ModLog.Id}/messages",
            FakeDiscordRest.Error(System.Net.HttpStatusCode.InternalServerError, 0, "Internal Server Error"));
        this._bot.DeleteMessage(messageId, this.Channel);

        var posts = await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this._bot.Rest.Requests
            .Where(this._bot.IsModLogPost).ToList()), found => found.Count >= 2, TimeSpan.FromSeconds(30));
        posts[^1].Files.ShouldHaveSingleItem().Value.ShouldNotBeEmpty();
        posts[^1].Files.Single().Value.ShouldBe(posts[0].Files.Single().Value);
    }

    private async Task<ulong> SendLoggedWithAttachmentsAsync(string content, params string[] fileNames)
    {
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Channel, content, fileNames);
        (await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = database.CreateDbContext();
            return await dbContext.Attachments.CountAsync(attachment => attachment.MessageId == new MessageId(messageId));
        }, count => count == fileNames.Length)).ShouldBe(fileNames.Length);
        return messageId;
    }

    private Task<RecordedRequest> WaitForDeleteLogPostAsync()
        => this._bot.Rest.WaitForRequestAsync(request => this._bot.IsModLogPost(request)
                                                         && request.Json?["embeds"]?[0]?["author"]?["name"]
                                                             ?.GetValue<string>() == $"Message deleted in #{this.Channel.Name}");

    private static List<(string, string)> FieldsOf(RecordedRequest post)
        => [.. post.Json!["embeds"]!.AsArray().SelectMany(embed => embed!["fields"]?.AsArray() ?? [])
            .Select(field => (field!["name"]!.GetValue<string>(), field["value"]!.GetValue<string>()))];

    /// <summary>What Discord counts toward the 6,000: title, description, field names and values, footer and author.</summary>
    private static int EmbedLength(System.Text.Json.Nodes.JsonNode embed)
        => (embed["title"]?.GetValue<string>().Length ?? 0)
           + (embed["description"]?.GetValue<string>().Length ?? 0)
           + (embed["footer"]?["text"]?.GetValue<string>().Length ?? 0)
           + (embed["author"]?["name"]?.GetValue<string>().Length ?? 0)
           + (embed["fields"]?.AsArray().Sum(field => field!["name"]!.GetValue<string>().Length
                                                      + field["value"]!.GetValue<string>().Length) ?? 0);

    /// <summary>I-1: a log post that times out loses that log only; the publisher keeps posting the rest.</summary>
    [Fact]
    public async Task AfterALogPostTimesOut_LaterLogsAreStillPosted()
    {
        var first = await this.SendLoggedAsync("times out");
        var second = await this.SendLoggedAsync("gets through");
        var timedOut = this._bot.Rest.TimeOutOnce(HttpMethod.Post, $"channels/{this.Guild.ModLog.Id}/messages");

        // One at a time: delete handlers run concurrently, so either log could otherwise take the timeout.
        this._bot.DeleteMessage(first, this.Channel);
        await timedOut.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        this._bot.DeleteMessage(second, this.Channel);

        await this._bot.Rest.WaitForRequestAsync(request => this._bot.IsModLogPost(request)
                                                            && request.Body?.Contains("gets through", StringComparison.Ordinal) == true);
    }

    /// <summary>L-2: the bot's own posts aren't saved, so purging its old log posts logs nothing.</summary>
    /// <remarks>Cases: LOG-63 (bot).</remarks>
    [Fact]
    public async Task TheBotsOwnMessages_AreNotSaved()
    {
        var botPost = this._bot.SendMessage(this.Guild.Bot, this.Channel, "a log post");
        await this.SendLoggedAsync("a member's message");
        await Task.Delay(NothingHappensWindow, TestContext.Current.CancellationToken);

        await using var dbContext = database.CreateDbContext();
        (await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(botPost), TestContext.Current.CancellationToken))
            .ShouldBeFalse();
    }

    /// <summary>
    ///     L-2: a post by the bot saved before it stopped saving them, and too old to be cached, is still recognised
    ///     by its stored author. Otherwise each purged log post would be logged as deleted.
    /// </summary>
    /// <remarks>Cases: LOG-97.</remarks>
    [Fact]
    public async Task Delete_OfAnUncachedPostByTheBot_IsIgnored()
    {
        const ulong botPost = 1_100_000_000_000_008_010;
        await using (var seed = database.CreateDbContext())
        {
            seed.Messages.Add(new Message
            {
                Id = new MessageId(botPost),
                UserId = new UserId(this.Guild.Bot.Id),
                GuildId = new GuildId(this.Guild.Id),
                ChannelId = new ChannelId(this.Channel.Id),
                CreatedTimestamp = DateTimeOffset.UtcNow.AddDays(-30)
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        this._bot.DeleteMessage(botPost, this.Channel);

        (await this.LoggedWithinAsync("Message deleted", NothingHappensWindow)).ShouldBeFalse();
        (await this.HistoryOfAsync<MessageDeletedEntry>(botPost)).ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>KI-39: when the audit log shows a moderator deleted it, that's recorded and shown.</summary>
    /// <remarks>Cases: LOG-86.</remarks>
    [Fact]
    public async Task Delete_ByAModerator_IsAttributed()
    {
        var messageId = await this.SendLoggedAsync("rule breaking");
        this._bot.Rest.AddAuditLogEntry(FakeDiscordRest.MessageDeleteAuditAction, this.Guild.Moderator, this.Guild.User1.Id, null,
            new JsonObject { ["channel_id"] = FakeUser.Snowflake(this.Channel.Id), ["count"] = "1" });

        this._bot.DeleteMessage(messageId, this.Channel);

        var embed = await this.WaitForLogAsync($"Message deleted in #{this.Channel.Name}");
        Field(embed, "Deleted By").ShouldBe(this.Guild.Moderator.MemberMention);
        (await this.HistoryOfAsync<MessageDeletedByModeratorEntry>(messageId)).ShouldHaveSingleItem()
            .ModeratorId.ShouldBe(new ModeratorId(this.Guild.Moderator.Id));
    }

    /// <summary>KI-51: the moderator id used to be mapped to a shadow property, so it was never saved.</summary>
    /// <remarks>Cases: LOG-86.</remarks>
    [Fact]
    public async Task DeletedByModeratorEntry_KeepsItsModerator()
    {
        var messageId = await this.SendLoggedAsync("rule breaking");
        var moderator = new ModeratorId(this.Guild.Moderator.Id);
        await using (var seed = database.CreateDbContext())
        {
            seed.MessageHistory.Add(new MessageDeletedByModeratorEntry
            {
                MessageId = new MessageId(messageId),
                GuildId = new GuildId(this.Guild.Id),
                ModeratorId = moderator,
                Timestamp = DateTimeOffset.UtcNow
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await this.HistoryOfAsync<MessageDeletedByModeratorEntry>(messageId)).ShouldHaveSingleItem().ModeratorId.ShouldBe(moderator);
    }

    /// <summary>Deleting a message the log never saved posts nothing.</summary>
    /// <remarks>Cases: LOG-96.</remarks>
    [Fact]
    public async Task Delete_OfAnUnloggedMessage_IsIgnored()
    {
        this._bot.DeleteMessage(1_100_000_000_000_008_003, this.Channel);

        (await this.LoggedWithinAsync("Message deleted", NothingHappensWindow)).ShouldBeFalse();
    }

    // ── Bulk deletes ──────────────────────────────────────────────────────────

    /// <summary>A bulk delete is recorded and logged once, with every message in an attached dump.</summary>
    /// <remarks>Cases: LOG-110.</remarks>
    [Fact]
    public async Task BulkDelete_IsRecorded_AndLoggedWithADump()
    {
        var ids = new[] { await this.SendLoggedAsync("one"), await this.SendLoggedAsync("two"), await this.SendLoggedAsync("three") };

        this._bot.BulkDeleteMessages(this.Channel, ids);

        var post = await this._bot.Rest.WaitForRequestAsync(request => this._bot.IsModLogPost(request)
                                                                      && request.Json?["embeds"]?[0]?["title"]?.GetValue<string>() == "Bulk Message Delete");
        post.Json!["embeds"]![0]!["description"]!.GetValue<string>()
            .ShouldBe($"**Message Count:** 3\n**Channel:** {this.Channel.Mention}\nFull message dump attached.");
        var dump = post.Files.ShouldHaveSingleItem().Value;
        foreach (var (id, content) in ids.Zip(["one", "two", "three"]))
            dump.ShouldContain($"Id: {id}\nContent: {content}\n");
        foreach (var id in ids)
            (await this.HistoryOfAsync<MessageDeletedEntry>(id)).ShouldHaveSingleItem();
    }

    /// <summary>L-9: the dump names each author, in message order, as a text file needs. A raw mention means nothing there.</summary>
    /// <remarks>Cases: LOG-110.</remarks>
    [Fact]
    public async Task BulkDelete_DumpNamesTheAuthors_InMessageOrder()
    {
        var ids = new[] { await this.SendLoggedAsync("one"), await this.SendLoggedAsync("two"), await this.SendLoggedAsync("three") };

        this._bot.BulkDeleteMessages(this.Channel, [.. ids.Reverse()]);

        var post = await this._bot.Rest.WaitForRequestAsync(request => this._bot.IsModLogPost(request)
                                                                      && request.Json?["embeds"]?[0]?["title"]?.GetValue<string>() == "Bulk Message Delete");
        var dump = post.Files.ShouldHaveSingleItem().Value;
        dump.ShouldContain($"Author: user1 ({this.Guild.User1.Id})\nId: {ids[0]}\n");
        dump.IndexOf("Content: one", StringComparison.Ordinal).ShouldBeLessThan(dump.IndexOf("Content: two", StringComparison.Ordinal));
        dump.IndexOf("Content: two", StringComparison.Ordinal).ShouldBeLessThan(dump.IndexOf("Content: three", StringComparison.Ordinal));
        this._bot.Rest.Requests.ShouldNotContain(request => request.Path.StartsWith($"guilds/{this.Guild.Id}/members/", StringComparison.Ordinal)
                                                            && request.Method == HttpMethod.Get);
    }

    /// <summary>L-7: with the delete log off, the history is still kept, but nothing is downloaded for a log nobody sees.</summary>
    /// <remarks>Cases: LOG-94.</remarks>
    [Fact]
    public async Task Delete_WithTheDeleteLogOff_RecordsIt_ButDownloadsNothing()
    {
        (await this._bot.Services.GetRequiredService<SettingsModule>().SetLogChannelSetting(GuildLogType.MessageDeleted,
            new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), null, cancellationToken: TestContext.Current.CancellationToken)).ShouldBeSetOrUnchanged();
        var messageId = await this.SendLoggedWithAttachmentsAsync("look", "cat.png");

        this._bot.DeleteMessage(messageId, this.Channel);

        (await GrimoireTestBot.EventuallyAsync(() => this.HistoryOfAsync<MessageDeletedEntry>(messageId), rows => rows.Count == 1))
            .ShouldHaveSingleItem();
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        this._bot.Cdn.Downloads.ShouldBeEmpty();
    }

    /// <summary>I-11: a log channel the bot can't post in is reported to the guild, not only logged for us.</summary>
    /// <remarks>Cases: LOG-79, ALRT-49.</remarks>
    [Fact]
    public async Task ALogChannelTheBotCannotPostIn_IsReportedToTheGuild()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureMessageLogAsync();
        await this.PointLogAtAsync(GuildLogType.MessageEdited, this.Guild.Private.Id);
        var messageId = await this.SendLoggedAsync("before");

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "after");

        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()?.StartsWith(
            $"I could not post in {this.Guild.Private.Mention}.", StringComparison.Ordinal) == true);
    }

    /// <summary>R-1: a log channel that was deleted is reported, so the guild can pick another.</summary>
    /// <remarks>Cases: LOG-80, ALRT-48.</remarks>
    [Fact]
    public async Task ADeletedLogChannel_IsReportedToTheGuild()
    {
        const ulong deletedChannel = 1_100_000_000_000_000_298;
        await this.PointLogAtAsync(GuildLogType.MessageEdited, deletedChannel);
        var messageId = await this.SendLoggedAsync("before");

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "after");

        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()?.StartsWith(
            $"The MessageEdited log is set to <#{deletedChannel}>, which no longer exists", StringComparison.Ordinal) == true);
    }

    private async Task PointLogAtAsync(GuildLogType logType, ulong channelId)
        => (await this._bot.Services.GetRequiredService<SettingsModule>().SetLogChannelSetting(logType,
            new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), new ChannelId(channelId))).ShouldBeSetOrUnchanged();

    /// <summary>L-11: a failed save loses the message from the log, so it's raised, not only written to the log file.</summary>
    /// <remarks>Cases: LOG-65.</remarks>
    [Fact]
    public async Task AMessageThatCannotBeSaved_IsRaisedAsAnError()
    {
        await using var dbContext = database.CreateDbContext();
        var messages = dbContext.Model.FindEntityType(typeof(Message))!;
        var table = $"\"{messages.GetSchema() ?? "public"}\".\"{messages.GetTableName()}\"";
        await dbContext.Database.ExecuteSqlRawAsync($"""ALTER TABLE {table} ADD CONSTRAINT "TestRefusesMessages" CHECK (false) NOT VALID""",
            TestContext.Current.CancellationToken);
        try
        {
            this._bot.SendMessage(this.Guild.User1, this.Channel, "lost");

            await this._bot.Logs.WaitForAsync(logEvent => logEvent.MessageTemplate.Text.StartsWith(
                "Unhandled exception in event handler", StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        }
        finally
        {
            await dbContext.Database.ExecuteSqlRawAsync($"""ALTER TABLE {table} DROP CONSTRAINT "TestRefusesMessages" """,
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>With the message log off, nothing is recorded or posted.</summary>
    /// <remarks>Cases: LOG-101.</remarks>
    [Fact]
    public async Task WithTheMessageLogOff_EditsAndDeletesAreIgnored()
    {
        var messageId = await this.SendLoggedAsync("before");
        await this._bot.SetModuleAsync(Module.MessageLog, false);

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "after");
        this._bot.DeleteMessage(messageId, this.Channel);

        (await this.LoggedWithinAsync("Message ", NothingHappensWindow)).ShouldBeFalse();
        (await this.HistoryOfAsync<MessageEditedEntry>(messageId)).ShouldBeEmpty();
        (await this.HistoryOfAsync<MessageDeletedEntry>(messageId)).ShouldBeEmpty();
    }

    /// <summary>L-3: after a restart the message isn't cached, but the stored author is enough for the mention.</summary>
    /// <remarks>Cases: LOG-98, XC-119.</remarks>
    [Fact]
    public async Task Delete_OfAnUncachedMessage_StillNamesTheAuthor()
    {
        const ulong oldMessage = 1_100_000_000_000_008_020;
        await using (var seed = database.CreateDbContext())
        {
            seed.Messages.Add(new Message
            {
                Id = new MessageId(oldMessage),
                UserId = new UserId(this.Guild.User1.Id),
                GuildId = new GuildId(this.Guild.Id),
                ChannelId = new ChannelId(this.Channel.Id),
                CreatedTimestamp = DateTimeOffset.UtcNow.AddDays(-3)
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        this._bot.DeleteMessage(oldMessage, this.Channel);

        Field(await this.WaitForLogAsync($"Message deleted in #{this.Channel.Name}"), "Author").ShouldBe(this.Guild.User1.Mention);
    }

    /// <summary>
    ///     L-5: Discord caps a message's embeds at 6,000 characters together, so a long edit goes out as two posts,
    ///     one with Before and one with After.
    /// </summary>
    /// <remarks>Cases: LOG-75.</remarks>
    [Fact]
    public async Task Edit_ThatIsLong_SendsBeforeAndAfterInSeparatePosts()
    {
        var before = string.Join(' ', Enumerable.Repeat("before", 430));
        var after = string.Join(' ', Enumerable.Repeat("after!", 430));
        var messageId = await this.SendLoggedAsync(before);

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, after);

        var posts = await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this._bot.Rest.Requests
                .Where(this._bot.IsModLogPost)
                .Select(request => request.Json!["embeds"]![0]!)
                .Where(embed => embed["author"]?["name"]?.GetValue<string>() == $"Message edited in #{this.Channel.Name}")
                .ToList()),
            found => found.Count >= 2);
        posts.Count.ShouldBe(2);
        FieldNames(posts[0]).ShouldContain("Before");
        FieldNames(posts[0]).ShouldNotContain("After");
        FieldNames(posts[1]).ShouldContain("After");
        FieldNames(posts[1]).ShouldNotContain("Before");
    }

    private async Task<ulong> SendLoggedAsync(string content)
    {
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Channel, content);
        (await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = database.CreateDbContext();
            return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId));
        }, saved => saved)).ShouldBeTrue();
        return messageId;
    }

    private Task<JsonNode> WaitForLogAsync(string title)
        => this._bot.WaitForModLogEmbedAsync(embed => embed["author"]?["name"]?.GetValue<string>() == title);

    private Task<bool> LoggedWithinAsync(string titleStart, TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                               && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString()
                                                                   .StartsWith(titleStart, StringComparison.Ordinal) == true, window);

    private async Task<List<T>> HistoryOfAsync<T>(ulong messageId) where T : MessageHistoryEntry
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.MessageHistory.OfType<T>().Where(entry => entry.MessageId == new MessageId(messageId))
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static string[] FieldNames(JsonNode embed)
        => [.. embed["fields"]!.AsArray().Select(field => field!["name"]!.GetValue<string>())];

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

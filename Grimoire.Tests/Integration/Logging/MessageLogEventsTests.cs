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

    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private FakeChannel Channel => this._bot.Guild.General;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
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
    [Fact(Skip = "Fails: the edit handler's query reads PluralKit ids through a null proxy link into non-nullable " +
                 "value types, so every edit of an ordinary message throws. KI-38")]
    public async Task Edit_IsRecorded_AndLoggedWithBeforeAndAfter()
    {
        var messageId = await this.SendLoggedAsync("before");

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "after");

        var embed = await this.WaitForLogAsync($"Message edited in #{this.Channel.Name}");
        Field(embed, "Channel").ShouldBe(this.Channel.Mention);
        Field(embed, "Message Id").ShouldBe(messageId.ToString());
        Field(embed, "Author").ShouldBe(this.Guild.User1.MemberMention);
        Field(embed, "Before").ShouldBe("before");
        Field(embed, "After").ShouldBe("after");
        (await this.HistoryOfAsync<MessageEditedEntry>(messageId)).ShouldHaveSingleItem().Content.ToString().ShouldBe("after");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>An edit of an unlogged message, and the bot's own edits, are ignored.</summary>
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
    [Fact(Skip = "Blocked: every edit of a logged message throws before the comparison is reached. KI-38")]
    public async Task Edit_ThatOnlyChangesCase_IsIgnored()
    {
        var messageId = await this.SendLoggedAsync("Hello There");

        this._bot.EditMessage(messageId, this.Guild.User1, this.Channel, "hello there");

        (await this.LoggedWithinAsync("Message edited", NothingHappensWindow)).ShouldBeFalse();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    // ── Deletes ───────────────────────────────────────────────────────────────

    /// <summary>A delete is recorded and logged with the author and the last known content.</summary>
    [Fact]
    public async Task Delete_IsRecorded_AndLoggedWithTheContent()
    {
        var messageId = await this.SendLoggedAsync("say goodbye");

        this._bot.DeleteMessage(messageId, this.Channel);

        var embed = await this.WaitForLogAsync($"Message deleted in #{this.Channel.Name}");
        Field(embed, "Author").ShouldBe(this.Guild.User1.MemberMention);
        Field(embed, "**Content**").ShouldBe("say goodbye");
        embed["fields"]!.AsArray().Select(f => f!["name"]!.GetValue<string>()).ShouldNotContain("Deleted By");
        (await this.HistoryOfAsync<MessageDeletedEntry>(messageId)).ShouldHaveSingleItem();
    }

    /// <summary>When the audit log shows a moderator deleted it, that's recorded and shown.</summary>
    [Fact(Skip = "Fails: this DSharpPlus nightly sets a message-delete audit entry's Target to the responsible user, so " +
                 "Grimoire's \"target is the author\" filter never matches. KI-39")]
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

    /// <summary>Deleting a message the log never saved posts nothing.</summary>
    [Fact]
    public async Task Delete_OfAnUnloggedMessage_IsIgnored()
    {
        this._bot.DeleteMessage(1_100_000_000_000_008_003, this.Channel);

        (await this.LoggedWithinAsync("Message deleted", NothingHappensWindow)).ShouldBeFalse();
    }

    // ── Bulk deletes ──────────────────────────────────────────────────────────

    /// <summary>A bulk delete is recorded and logged once, with every message in an attached dump.</summary>
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

    /// <summary>With the message log off, nothing is recorded or posted.</summary>
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

    private async Task<ulong> SendLoggedAsync(string content)
    {
        var messageId = this._bot.SendMessage(this.Guild.User1, this.Channel, content);
        (await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
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
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.MessageHistory.OfType<T>().Where(entry => entry.MessageId == new MessageId(messageId))
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

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
///     Who the delete log says deleted a message (backlog A-44, area 05 section G). Discord merges a moderator's
///     deletes of one author's messages in one channel into a single audit entry whose count goes up. The bot
///     remembers the count it last saw for each entry, and credits a delete to the moderator only when the count rose.
/// </summary>
[Collection("Test collection")]
public sealed class MessageDeleteAttributionTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private FakeChannel Channel => this._bot.Guild.General;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(database);
        await this.ConfigureAsync();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task ConfigureAsync()
    {
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureMessageLogAsync();
        (await this._bot.Services.GetRequiredService<SettingsModule>().SetLogChannelSetting(GuildLogType.MessageDeleted,
            new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), new ChannelId(this.Guild.ModLog.Id))).ShouldBeSetOrUnchanged();
    }

    /// <summary>LOG-87: two deletes by the same moderator raise the entry's count to 1, then 2. Both are credited.</summary>
    [Fact]
    public async Task TwoDeletesByAModerator_AreBothAttributed()
    {
        var first = await this.SendLoggedAsync("first");
        var second = await this.SendLoggedAsync("second");
        var options = this.ModeratorDeletes(count: 1);

        this._bot.DeleteMessage(first, this.Channel);
        (await this.WaitForDeleteLogAsync("first")).ShouldContain(("Deleted By", this.Guild.Moderator.MemberMention));

        // Discord updates the same entry for a second delete; the fake serves the options object it was given.
        options["count"] = "2";
        this._bot.DeleteMessage(second, this.Channel);

        (await this.WaitForDeleteLogAsync("second")).ShouldContain(("Deleted By", this.Guild.Moderator.MemberMention));
        foreach (var messageId in new[] { first, second })
            (await this.HistoryOfAsync<MessageDeletedByModeratorEntry>(messageId)).ShouldHaveSingleItem()
                .ModeratorId.ShouldBe(new ModeratorId(this.Guild.Moderator.Id));
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>LOG-88: after a moderator's delete, the author's own delete in that channel doesn't raise the count.</summary>
    [Fact]
    public async Task ASelfDelete_AfterAModeratorsDelete_IsNotAttributed()
    {
        var byModerator = await this.SendLoggedAsync("removed by mod");
        var bySelf = await this.SendLoggedAsync("removed by me");
        this.ModeratorDeletes(count: 1);
        this._bot.DeleteMessage(byModerator, this.Channel);
        (await this.WaitForDeleteLogAsync("removed by mod")).ShouldContain(("Deleted By", this.Guild.Moderator.MemberMention));

        this._bot.DeleteMessage(bySelf, this.Channel);

        (await this.WaitForDeleteLogAsync("removed by me")).Select(field => field.Name).ShouldNotContain("Deleted By");
        (await this.HistoryOfAsync<MessageDeletedByModeratorEntry>(bySelf)).ShouldBeEmpty();
        (await this.HistoryOfAsync<MessageDeletedEntry>(bySelf)).ShouldHaveSingleItem();
    }

    /// <summary>
    ///     LOG-89: the moderator deleted a message the log never saved, so the bot never read that audit entry. The
    ///     author's own delete of a saved message a moment later finds the entry with a count it hasn't seen.
    /// </summary>
    [Fact(Skip = "Fails: a mod's delete of an unsaved message never moves the cached audit count, so the author's later self-delete is credited to the mod. KI-64")]
    public async Task ASelfDelete_AfterAModeratorDeletedAnUnsavedMessage_IsNotAttributed()
    {
        var unsaved = this._bot.NextSnowflake();
        var saved = await this.SendLoggedAsync("removed by me");
        this.ModeratorDeletes(count: 1);
        this._bot.DeleteMessage(unsaved, this.Channel);
        (await this.LoggedWithinAsync(NothingHappensWindow)).ShouldBeFalse("the unsaved message isn't logged");

        this._bot.DeleteMessage(saved, this.Channel);

        (await this.WaitForDeleteLogAsync("removed by me")).Select(field => field.Name).ShouldNotContain("Deleted By");
        (await this.HistoryOfAsync<MessageDeletedByModeratorEntry>(saved)).ShouldBeEmpty();
    }

    /// <summary>LOG-95: without View Audit Log the delete is logged unattributed, and the guild hears why.</summary>
    [Fact]
    public async Task WithoutViewAuditLog_TheDeleteIsLoggedUnattributed_AndReported()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        await this.ConfigureAsync();
        var messageId = await this.SendLoggedAsync("rule breaking");
        this.ModeratorDeletes(count: 1);

        this._bot.DeleteMessage(messageId, this.Channel);

        (await this.WaitForDeleteLogAsync("rule breaking")).Select(field => field.Name).ShouldNotContain("Deleted By");
        await this._bot.WaitForModLogEmbedAsync(embed => embed["description"]?.GetValue<string>()?.StartsWith(
            "I cannot read the audit log, so I cannot tell who deleted messages or banned members outside my commands.",
            StringComparison.Ordinal) == true);
        (await this.HistoryOfAsync<MessageDeletedEntry>(messageId)).ShouldHaveSingleItem();
        (await this.HistoryOfAsync<MessageDeletedByModeratorEntry>(messageId)).ShouldBeEmpty();
    }

    /// <summary>A message-delete audit entry: the moderator deleted <c>user1</c>'s messages in <c>#general</c>.</summary>
    private JsonObject ModeratorDeletes(int count)
    {
        var options = new JsonObject { ["channel_id"] = FakeUser.Snowflake(this.Channel.Id), ["count"] = count.ToString() };
        this._bot.Rest.AddAuditLogEntry(FakeDiscordRest.MessageDeleteAuditAction, this.Guild.Moderator, this.Guild.User1.Id, null,
            options);
        return options;
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

    /// <summary>The fields of the delete log for the message with this content.</summary>
    private async Task<List<(string Name, string Value)>> WaitForDeleteLogAsync(string content)
    {
        var embed = await this._bot.WaitForModLogEmbedAsync(embed =>
            embed["author"]?["name"]?.GetValue<string>() == $"Message deleted in #{this.Channel.Name}"
            && embed["fields"]?.AsArray().Any(field => field?["name"]?.GetValue<string>() == "**Content**"
                                                       && field["value"]?.GetValue<string>() == content) == true);
        return [.. embed["fields"]!.AsArray().Select(field => (field!["name"]!.GetValue<string>(), field["value"]!.GetValue<string>()))];
    }

    private Task<bool> LoggedWithinAsync(TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                               && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString()
                                                                   .StartsWith("Message deleted", StringComparison.Ordinal) == true, window);

    private async Task<List<T>> HistoryOfAsync<T>(ulong messageId) where T : MessageHistoryEntry
    {
        await using var dbContext = database.CreateDbContext();
        return await dbContext.MessageHistory.OfType<T>().Where(entry => entry.MessageId == new MessageId(messageId))
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Tests.Integration.FakeDiscord;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     <c>/Purge All</c> and <c>/Purge User</c>, section E of <c>docs/testing/01-general-settings.md</c>. The channel's
///     history is seeded straight into the fake. GEN-139 is enforced by Discord's client, GEN-141, 143 and 146 stay
///     manual, GEN-147's message-log half is in A-22, and GEN-155 (PluralKit) is in A-34.
/// </summary>
[Collection("Test collection")]
public sealed class PurgeCommandsTests(GrimoireCoreFactory database) : IAsyncLifetime
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
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    // ── Purge All ─────────────────────────────────────────────────────────────

    /// <summary>GEN-130: the newest messages go in one bulk delete, with the reply ephemeral and the reason set.</summary>
    [Fact]
    public async Task All_DeletesTheNewestMessagesInOneBulkDelete()
    {
        var ids = this.SeedRecent(10, this.Guild.User1);

        var reply = await this.PurgeAllAsync(5);

        reply.Description.ShouldBe("Purged 5 messages");
        reply.IsEphemeral.ShouldBeTrue();
        var bulk = this._bot.Rest.Requests.Single(this.IsBulkDelete);
        BulkIds(bulk).ShouldBe(ids[^5..], ignoreOrder: true);
        bulk.AuditLogReason.ShouldBe($"{this.Guild.Moderator.Username} purged these messages.");
        this._bot.Rest.HistoryIds(this.Channel).ShouldBe(ids[..5]);
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>GEN-131: one message uses the single delete, since bulk delete needs 2 to 100.</summary>
    [Fact]
    public async Task All_WithCountOne_UsesTheSingleDelete()
    {
        var ids = this.SeedRecent(3, this.Guild.User1);

        (await this.PurgeAllAsync(1)).Description.ShouldBe("Purged 1 message");

        this._bot.Rest.Requests.Count(this.IsBulkDelete).ShouldBe(0);
        this._bot.Rest.HistoryIds(this.Channel).ShouldBe(ids[..2]);
    }

    /// <summary>GEN-132, GEN-133 and GEN-134.</summary>
    [Theory]
    [InlineData(10, 0, 0)]
    [InlineData(3, 50, 3)]
    [InlineData(0, 10, 0)]
    public async Task All_DeletesAtMostWhatExists(int seeded, int count, int expected)
    {
        this.SeedRecent(seeded, this.Guild.User1);

        (await this.PurgeAllAsync(count)).Description.ShouldBe($"Purged {expected} {(expected == 1 ? "message" : "messages")}");

        this._bot.Rest.HistoryIds(this.Channel).Count.ShouldBe(seeded - expected);
    }

    /// <summary>GEN-137.</summary>
    [Fact]
    public async Task All_WithExactlyOneHundred_UsesOneBulkDelete()
    {
        this.SeedRecent(100, this.Guild.User1);

        (await this.PurgeAllAsync(100)).Description.ShouldBe("Purged 100 messages");

        this._bot.Rest.Requests.Count(this.IsBulkDelete).ShouldBe(1);
        this._bot.Rest.HistoryIds(this.Channel).ShouldBeEmpty();
    }

    /// <summary>GEN-135 and GEN-136: more than 100 messages should be deleted in chunks of 100.</summary>
    [Theory(Skip = "Fails: PurgeMessagesAsync calls GetMessagesAsync() with DSharpPlus's default limit of 100, so it " +
                   "never sees more than the newest 100 messages. KI-24")]
    [InlineData(250)]
    [InlineData(101)]
    public async Task All_WithMoreThanOneHundred_DeletesThemAll(int count)
    {
        this.SeedRecent(count, this.Guild.User1);

        (await this.PurgeAllAsync(count)).Description.ShouldBe($"Purged {count} messages");

        this._bot.Rest.HistoryIds(this.Channel).ShouldBeEmpty();
    }

    /// <summary>GEN-138: messages older than 14 days can't be bulk deleted, so they are left alone.</summary>
    [Fact]
    public async Task All_LeavesMessagesOlderThanFourteenDays()
    {
        var old = this._bot.Rest.SeedHistory(this.Channel,
            [.. Enumerable.Range(0, 3).Select(i => (this.Guild.User1, DateTimeOffset.UtcNow.AddDays(-20).AddMinutes(i)))]);
        this.SeedRecent(3, this.Guild.User1);

        (await this.PurgeAllAsync(10)).Description.ShouldBe("Purged 3 messages");

        this._bot.Rest.HistoryIds(this.Channel).ShouldBe(old);
    }

    /// <summary>GEN-140: without Manage Messages the bot refuses, and nothing is deleted.</summary>
    [Fact]
    public async Task All_WhenTheBotLacksManageMessages_IsRefused()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, guild => guild.BotPermissions =
            FakeGuild.ViewChannelPermission | FakeGuild.SendMessagesPermission | FakeGuild.EmbedLinksPermission);
        this.SeedRecent(3, this.Guild.User1);

        (await this.PurgeAllAsync(3)).Description.ShouldBe("The bot did not have the needed permissions to execute this command.");

        this._bot.Rest.HistoryIds(this.Channel).Count.ShouldBe(3);
    }

    /// <summary>GEN-142 and GEN-156: Discord only offers the command to members with Manage Messages.</summary>
    [Fact]
    public void Purge_IsRegisteredForManageMessagesOnly()
        => this._bot.Rest.RegisteredCommands["purge"]["default_member_permissions"]!.ToString()
            .ShouldBe(FakeGuild.ManageMessagesPermission.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>GEN-145.</summary>
    [Fact]
    public async Task All_WorksInsideAThread()
    {
        this._bot.Rest.SeedHistory(this.Guild.PublicThread, [.. RecentTimes(4).Select(at => (this.Guild.User1, at))]);

        (await FakeReply.WaitForAsync(this._bot.Rest, this._bot.RunSlashCommand(this.Guild.PublicThread, "purge",
                this.Guild.Moderator, SlashOption.Subcommand("all", SlashOption.Integer("count", 4)))))
            .Description.ShouldBe("Purged 4 messages");

        this._bot.Rest.HistoryIds(this.Guild.PublicThread).ShouldBeEmpty();
    }

    /// <summary>GEN-147, area 01 code observation 9: purging leaves no mod-log entry.</summary>
    [Fact]
    public async Task All_PostsNothingToTheModLog()
    {
        await this._bot.ConfigureModerationAsync();
        this.SeedRecent(3, this.Guild.User1);

        await this.PurgeAllAsync(3);

        (await this._bot.Rest.ReceivesRequestWithinAsync(this._bot.IsModLogPost, NothingHappensWindow)).ShouldBeFalse();
    }

    // ── Purge User ────────────────────────────────────────────────────────────

    /// <summary>GEN-150: only the target's newest messages go, and the reason names both members.</summary>
    [Fact]
    public async Task User_DeletesOnlyTheTargetsNewestMessages()
    {
        var ids = this.SeedAlternating(10);
        var user1Ids = ids.Where((_, index) => index % 2 == 0).ToArray();

        (await this.PurgeUserAsync(this.Guild.User1, 3)).Description.ShouldBe("Purged 3 messages");

        var bulk = this._bot.Rest.Requests.Single(this.IsBulkDelete);
        BulkIds(bulk).ShouldBe(user1Ids[^3..], ignoreOrder: true);
        bulk.AuditLogReason.ShouldBe($"{this.Guild.Moderator.MemberMention} purged the messages of {this.Guild.User1.Mention}.");
        this._bot.Rest.HistoryIds(this.Channel).ShouldBe(ids.Except(user1Ids[^3..]));
    }

    /// <summary>GEN-151, GEN-152 and GEN-157.</summary>
    [Theory]
    [InlineData(100, 5)]
    [InlineData(1, 1)]
    public async Task User_DeletesAtMostTheTargetsMessages(int count, int expected)
    {
        this.SeedAlternating(10);

        (await this.PurgeUserAsync(this.Guild.User1, count)).Description
            .ShouldBe($"Purged {expected} {(expected == 1 ? "message" : "messages")}");

        this._bot.Rest.HistoryIds(this.Channel).Count.ShouldBe(10 - expected);
    }

    [Fact]
    public async Task User_WithNoMessages_PurgesNothing()
    {
        this.SeedRecent(4, this.Guild.Moderator);

        (await this.PurgeUserAsync(this.Guild.User1, 10)).Description.ShouldBe("Purged 0 messages");
        this._bot.Rest.HistoryIds(this.Channel).Count.ShouldBe(4);
    }

    /// <summary>GEN-153 and GEN-154: authors are matched by identity, for users who left and for the bot.</summary>
    [Theory]
    [InlineData("left")]
    [InlineData("bot")]
    public async Task User_MatchesAuthorsWhoAreNotOrdinaryMembers(string who)
    {
        var target = who == "bot" ? this.Guild.Bot : new FakeUser(1_100_000_000_000_000_098, "departed");
        this._bot.Rest.SeedHistory(this.Channel, [.. RecentTimes(3).Select(at => (target, at))]);
        this.SeedRecent(2, this.Guild.User1);

        (await this.PurgeUserAsync(target, 10)).Description.ShouldBe("Purged 3 messages");
        this._bot.Rest.HistoryIds(this.Channel).Count.ShouldBe(2);
    }

    /// <summary>GEN-158: the scan stops at the first of the target's messages older than 14 days.</summary>
    [Fact]
    public async Task User_LeavesTheTargetsMessagesOlderThanFourteenDays()
    {
        var old = this._bot.Rest.SeedHistory(this.Channel, (this.Guild.User1, DateTimeOffset.UtcNow.AddDays(-20)));
        this.SeedRecent(2, this.Guild.User1);

        (await this.PurgeUserAsync(this.Guild.User1, 10)).Description.ShouldBe("Purged 2 messages");
        this._bot.Rest.HistoryIds(this.Channel).ShouldBe(old);
    }

    /// <summary>The target's messages behind the newest 100 in the channel should still be found.</summary>
    [Fact(Skip = "Fails: only the newest 100 messages in the channel are scanned, so older messages from the target " +
                 "are never found. KI-24")]
    public async Task User_FindsTheTargetsMessagesBehindTheNewestHundred()
    {
        this._bot.Rest.SeedHistory(this.Channel, [.. RecentTimes(2, minutesAgo: 300).Select(at => (this.Guild.User1, at))]);
        this.SeedRecent(150, this.Guild.Moderator);

        (await this.PurgeUserAsync(this.Guild.User1, 10)).Description.ShouldBe("Purged 2 messages");
    }

    private Task<FakeReply> PurgeAllAsync(int count)
        => this._bot.RunSlashCommandAndWaitAsync("purge", this.Guild.Moderator,
            SlashOption.Subcommand("all", SlashOption.Integer("count", count)));

    private Task<FakeReply> PurgeUserAsync(FakeUser target, int count)
        => this._bot.RunSlashCommandAndWaitAsync("purge", this.Guild.Moderator,
            SlashOption.Subcommand("user", SlashOption.User("user", target), SlashOption.Integer("count", count)));

    /// <summary>Recent messages, oldest first, a minute apart and all within the last few hours.</summary>
    private ulong[] SeedRecent(int count, FakeUser author)
        => this._bot.Rest.SeedHistory(this.Channel, [.. RecentTimes(count).Select(at => (author, at))]);

    /// <summary>Messages alternating between user1 and the moderator, oldest first, starting with user1.</summary>
    private ulong[] SeedAlternating(int count)
        => this._bot.Rest.SeedHistory(this.Channel,
            [.. RecentTimes(count).Select((at, index) => (index % 2 == 0 ? this.Guild.User1 : this.Guild.Moderator, at))]);

    private static IEnumerable<DateTimeOffset> RecentTimes(int count, int minutesAgo = 250)
        => Enumerable.Range(0, count).Select(i => DateTimeOffset.UtcNow.AddMinutes(-minutesAgo + i));

    private bool IsBulkDelete(RecordedRequest request)
        => request.Method == HttpMethod.Post && request.Path == $"channels/{this.Channel.Id}/messages/bulk-delete";

    private static ulong[] BulkIds(RecordedRequest request)
        => [.. request.Json!["messages"]!.AsArray().Select(id => ulong.Parse(id!.ToString()))];
}

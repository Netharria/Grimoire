// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json.Nodes;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     PluralKit beyond the message log's basics (backlog A-50, area 09 sections A and C). <c>user1</c> plays
///     <c>pk</c>: PluralKit deletes their original and reposts it through a webhook, which is a bot author with no
///     member. Leveling, the spam filter, custom commands and thread locks never ask PluralKit; they skip the webhook
///     because it isn't a member. The message log points at <c>#modlog</c>, and PluralKit is configured unless a test
///     restarts the bot without it.
/// </summary>
[Collection("Test collection")]
public sealed class PluralKitAcrossFeaturesTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private readonly FakePluralKit _pluralKit = new();
    private readonly FakeUser _webhook = new(1_100_000_000_000_006_001, "Alice", IsBot: true);
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private FakeChannel Channel => this._bot.Guild.General;
    private FakeUser Pk => this._bot.Guild.User1;
    private SettingsModule Settings => this._bot.Services.GetRequiredService<SettingsModule>();
    private GuildId GuildId => new(this.Guild.Id);
    private ModeratorId Owner => new(this.Guild.Owner.Id);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        await this.StartAsync(new Dictionary<string, string?> { ["pluralkitToken"] = "fake-token" });
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private async Task StartAsync(Dictionary<string, string?> configuration)
    {
        if (this._bot is not null)
            await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database, configureHost: builder =>
        {
            builder.Configuration.AddInMemoryCollection(configuration);
            builder.Services.AddHttpClient("Pluralkit").ConfigurePrimaryHttpMessageHandler(() => this._pluralKit);
        });
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureMessageLogAsync();
        (await this.Settings.SetLogChannelSetting(GuildLogType.MessageDeleted, this.GuildId, this.Owner,
            new ChannelId(this.Guild.ModLog.Id))).ShouldBeSetOrUnchanged();
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>
    ///     XC-07: with the token and user agent set but no API endpoint, PluralKit should count as not configured and
    ///     the delete be logged as usual.
    /// </summary>
    [Fact(Skip = "Fails: PluralKit counts as configured without pluralkitApiEndpoint, and CreateClient throws outside the try, so the delete isn't logged. KI-101")]
    public async Task WithoutAnApiEndpoint_AQuickDelete_IsStillLogged()
    {
        // The harness sets pluralkitUserAgent; an empty value here hides its endpoint, as if the key were removed.
        await this.StartAsync(new Dictionary<string, string?>
        {
            ["pluralkitToken"] = "fake-token",
            ["pluralkitApiEndpoint"] = ""
        });
        var messageId = await this.SendLoggedAsync(this.Pk, "plain");

        this._bot.DeleteMessage(messageId, this.Channel);

        (await this.DeleteLoggedWithinAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.MessageHistory.OfType<MessageDeletedEntry>().AnyAsync(Token)).ShouldBeTrue();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    // ── Message log ───────────────────────────────────────────────────────────

    /// <summary>XC-32: a proxy deleted over a minute later is logged from the stored link, without asking PluralKit.</summary>
    [Fact]
    public async Task DeletingAnOldProxy_UsesTheStoredLink_WithoutALookup()
    {
        var original = await this.SendLoggedAsync(this.Pk, "a:hello");
        var proxy = await this.SendOldProxyAsync("hello", DateTimeOffset.UtcNow.AddMinutes(-2));
        this._pluralKit.AddProxy(original, proxy, this.Pk, this.Guild, "abcde", "fghij");
        this._bot.DeleteMessage(original, this.Channel);
        await GrimoireTestBot.EventuallyAsync(this.LinkCountAsync, count => count > 0);

        this._bot.DeleteMessage(proxy, this.Channel);

        var embed = await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == $"Message deleted in #{this.Channel.Name}");
        Field(embed, "Original Author").ShouldBe(this.Pk.Mention);
        Field(embed, "System Id").ShouldBe("abcde");
        Field(embed, "Member Id").ShouldBe("fghij");
        this._pluralKit.Lookups.ShouldNotContain($"messages/{proxy}");
        this._bot.Rest.Unhandled.ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>
    ///     XC-37: five proxies in a second. Each original should be linked, not logged as deleted. The lookup is limited
    ///     to two a second with no queue, so the others are logged as deletes by <c>pk</c> and their proxies get no link.
    /// </summary>
    [Fact(Skip = "Fails: the PluralKit rate limiter (2 a second, no queue) refuses the other lookups, so 3 of 5 originals are logged as deleted and their proxies get no link. KI-111")]
    public async Task FiveQuickProxies_AreAllLinked_AndNoneLoggedAsDeleted()
    {
        var pairs = new List<(ulong Original, ulong Proxy)>();
        for (var i = 0; i < 5; i++)
            pairs.Add(await this.ProxyAsync($"hello {i}"));

        foreach (var (original, _) in pairs)
            this._bot.DeleteMessage(original, this.Channel);

        (await GrimoireTestBot.EventuallyAsync(this.LinkCountAsync, count => count >= pairs.Count)).ShouldBe(pairs.Count);
        (await this.DeleteLoggedWithinAsync(NothingHappensWindow)).ShouldBeFalse();
    }

    /// <summary>XC-39: in a channel the message log never logs, neither message is saved and PluralKit isn't asked.</summary>
    [Fact]
    public async Task InANeverLoggedChannel_NothingIsSaved_AndPluralKitIsNotAsked()
    {
        (await this.Settings.SetChannelLogOverride(new ChannelId(this.Channel.Id), this.GuildId, this.Owner,
            MessageLogOverrideOption.NeverLog, Token)).ShouldBeSetOrUnchanged();
        var original = this._bot.SendMessage(this.Pk, this.Channel, "a:hello");
        var proxy = this._bot.SendMessage(this._webhook, this.Channel, "hello");
        this._pluralKit.AddProxy(original, proxy, this.Pk, this.Guild, "abcde", "fghij");

        this._bot.DeleteMessage(original, this.Channel);

        (await this.DeleteLoggedWithinAsync(NothingHappensWindow)).ShouldBeFalse();
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Messages.AnyAsync(Token)).ShouldBeFalse();
        this._pluralKit.Lookups.ShouldBeEmpty();
    }

    // ── Leveling ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     XC-40, XC-41 and LVL-20: the original earns <c>pk</c> XP once, the proxy earns nothing for the webhook, and
    ///     a second proxied message inside the timeout earns nothing, because the timeout is per account.
    /// </summary>
    [Fact]
    public async Task ProxiedMessages_EarnXpOnlyForTheMember_OncePerTimeout()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);

        await this.ProxyAsync("hello");
        (await this.WaitForXpAsync(this.Pk, 1)).Count.ShouldBe(1);
        await this.ProxyAsync("hello again");

        (await this.WaitForXpAsync(this.Pk, 2, NothingHappensWindow)).Count.ShouldBe(1);
        (await this.WaitForXpAsync(this._webhook, 1, TimeSpan.FromMilliseconds(200))).ShouldBeEmpty();
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>XC-42: levelling up through proxied messages rewards <c>pk</c>, and the log names them.</summary>
    [Fact]
    public async Task LevellingUpThroughProxies_RewardsTheMember()
    {
        await this._bot.SetModuleAsync(Module.Leveling, true);
        (await this.Settings.SetLogChannelSetting(GuildLogType.Leveling, this.GuildId, this.Owner,
            new ChannelId(this.Guild.LevelLog.Id), Token)).ShouldBeSetOrUnchanged();
        (await this.Settings.SetLevelingSettings(this.GuildId, this.Owner, LevelSettings.Amount, 15, Token)).ShouldSucceed();
        (await this.Settings.SetRewardAsync(RewardAdded.Create(new RoleId(this.Guild.KickerRoleId), this.GuildId, 2, null,
            this.Owner, DateTimeOffset.UtcNow).ShouldSucceed(), Token)).ShouldSucceed();

        await this.ProxyAsync("level me");

        await this._bot.WaitForLevelLogEmbedAsync(e => e["description"]?.GetValue<string>()
            == $"{this.Pk.MemberMention} has earned <@&{this.Guild.KickerRoleId}>");
        this.Guild.GetMember(this.Pk).RoleIds.ShouldContain(this.Guild.KickerRoleId);
        await Task.Delay(TimeSpan.FromMilliseconds(500), Token);
        (await this.WaitForXpAsync(this._webhook, 1, TimeSpan.FromMilliseconds(200))).ShouldBeEmpty();
    }

    // ── Spam filter ───────────────────────────────────────────────────────────

    /// <summary>
    ///     XC-43 and SPAM-31: spamming through proxies mutes <c>pk</c> once from the originals. The filter's delete of an
    ///     original fails quietly because PluralKit got there first, and the proxies stay, because webhooks are skipped.
    /// </summary>
    [Fact]
    public async Task SpamThroughProxies_MutesTheMember_AndLeavesTheProxies()
    {
        await this._bot.SetModuleAsync(Module.AntiSpam, true);
        var muteRoleId = this._bot.Rest.CreateRole("Muted");
        (await this.Settings.SetMuteRole(this.GuildId, this.Owner, new RoleId(muteRoleId), Token)).ShouldSucceed();
        var proxies = new List<ulong>();
        this._bot.Rest.On(HttpMethod.Delete, @"channels/\d+/messages/\d+",
            _ => FakeDiscordRest.Error(HttpStatusCode.NotFound, 10008, "Unknown Message"));

        for (var i = 0; i < 6; i++)
        {
            var original = this._bot.SendMessage(this.Pk, this.Channel, "a:buy cheap gold now");
            var proxy = this._bot.SendMessage(this._webhook, this.Channel, "buy cheap gold now");
            this._pluralKit.AddProxy(original, proxy, this.Pk, this.Guild, "abcde", "fghij");
            proxies.Add(proxy);
            await Task.Delay(TimeSpan.FromMilliseconds(30), Token);
        }

        await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Put
                                                            && request.Path.EndsWith($"/members/{this.Pk.Id}/roles/{muteRoleId}", StringComparison.Ordinal));
        await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == "Mute");
        await Task.Delay(NothingHappensWindow, Token);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.Sins.ToListAsync(Token)).ShouldHaveSingleItem().UserId.ShouldBe(new UserId(this.Pk.Id));
        this._bot.Rest.Requests.Where(request => request.Method == HttpMethod.Delete)
            .Select(request => request.Path)
            .ShouldNotContain(path => proxies.Any(proxy => path == $"channels/{this.Channel.Id}/messages/{proxy}"));
    }

    // ── Custom commands ───────────────────────────────────────────────────────

    /// <summary>
    ///     XC-44 and CC-125: an original that starts with the trigger, as with an auto-proxy, runs the command once. With
    ///     a prefix tag the original doesn't start with it. The proxy never runs it, because the webhook is a bot.
    /// </summary>
    [Theory]
    [InlineData("!hello", 1)]
    [InlineData("a:!hello", 0)]
    public async Task AProxiedTrigger_RunsOnlyFromTheOriginal(string original, int expectedPosts)
    {
        await this._bot.SetModuleAsync(Module.Commands, true);
        await this._bot.RunSlashCommandAndWaitAsync("commands", this.Guild.Admin, SlashOption.Subcommand("learn",
            SlashOption.String("name", "hello"), SlashOption.String("content", "Hello there!")));

        this._bot.SendMessage(this.Pk, this.Channel, original);
        this._bot.SendMessage(this._webhook, this.Channel, "!hello");

        if (expectedPosts > 0)
            await this._bot.Rest.WaitForRequestAsync(this.IsPostInChannel);
        await Task.Delay(NothingHappensWindow, Token);
        this._bot.Rest.Requests.Count(this.IsPostInChannel).ShouldBe(expectedPosts);
        await using var dbContext = this._database.CreateDbContext();
        (await dbContext.CustomCommandUsages.ToListAsync(Token)).Select(use => use.UserId.Value)
            .ShouldBe(Enumerable.Repeat(this.Pk.Id, expectedPosts));
    }

    // ── Thread locks ──────────────────────────────────────────────────────────

    /// <summary>XC-46: in a locked thread, the lock deletes <c>pk</c>'s original.</summary>
    [Fact]
    public async Task InALockedThread_TheOriginalIsDeleted()
    {
        var thread = this.Guild.PublicThread;
        await this._bot.LockThreadAsync(thread, TimeSpan.FromMinutes(2));

        var original = this._bot.SendMessage(this.Pk, thread, "a:can I still post?");
        this._bot.SendMessage(this._webhook, thread, "can I still post?");

        (await this._bot.Rest.WaitForRequestAsync(request => request.Method == HttpMethod.Delete
                                                             && request.Path == $"channels/{thread.Id}/messages/{original}"))
            .AuditLogReason.ShouldBe("Thread is locked.");
    }

    /// <summary>
    ///     XC-46: the proxy should be deleted as well, as a bot's message would be, or a locked member can still post
    ///     through PluralKit. The lock handler skips any author that isn't a member, so the webhook's proxy stays.
    /// </summary>
    [Fact(Skip = "Fails: LockedThreadEvents returns for authors that aren't members, so PluralKit's webhook proxy stays in a locked thread. KI-112")]
    public async Task InALockedThread_TheProxyIsDeletedToo()
    {
        var thread = this.Guild.PublicThread;
        await this._bot.LockThreadAsync(thread, TimeSpan.FromMinutes(2));

        this._bot.SendMessage(this.Pk, thread, "a:can I still post?");
        var proxy = this._bot.SendMessage(this._webhook, thread, "can I still post?");

        (await this._bot.Rest.ReceivesRequestWithinAsync(request => request.Method == HttpMethod.Delete
                                                                    && request.Path == $"channels/{thread.Id}/messages/{proxy}",
            NothingHappensWindow)).ShouldBeTrue();
    }

    // ── Purge ─────────────────────────────────────────────────────────────────

    /// <summary>XC-47 and GEN-155: <c>/Purge User</c> on <c>pk</c> leaves the proxies, whose author is the webhook.</summary>
    [Fact]
    public async Task PurgeUser_OnTheMember_LeavesTheProxies()
    {
        var ids = this._bot.Rest.SeedHistory(this.Channel, [.. Enumerable.Range(0, 6)
            .Select(i => (i % 2 == 0 ? this.Pk : this._webhook, DateTimeOffset.UtcNow.AddMinutes(-60 + i)))]);

        var reply = await this._bot.RunSlashCommandAndWaitAsync("purge", this.Guild.Moderator,
            SlashOption.Subcommand("user", SlashOption.User("user", this.Pk), SlashOption.Integer("count", 10)));

        reply.Description.ShouldBe("Purged 3 messages");
        this._bot.Rest.HistoryIds(this.Channel).ShouldBe([ids[1], ids[3], ids[5]]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>The original, then the proxy PluralKit posts in its place, both saved by the message log.</summary>
    private async Task<(ulong Original, ulong Proxy)> ProxyAsync(string content)
    {
        var original = await this.SendLoggedAsync(this.Pk, $"a:{content}");
        var proxy = await this.SendLoggedAsync(this._webhook, content);
        this._pluralKit.AddProxy(original, proxy, this.Pk, this.Guild, "abcde", "fghij");
        return (original, proxy);
    }

    private async Task<ulong> SendLoggedAsync(FakeUser author, string content)
        => await this.WaitUntilSavedAsync(this._bot.SendMessage(author, this.Channel, content));

    /// <summary>A proxy posted at <paramref name="at" />, so its snowflake dates it then, saved by the message log.</summary>
    private async Task<ulong> SendOldProxyAsync(string content, DateTimeOffset at)
    {
        const long discordEpochMilliseconds = 1_420_070_400_000;
        var messageId = (ulong)(at.ToUnixTimeMilliseconds() - discordEpochMilliseconds) << 22;
        this._bot.Gateway.Dispatch("MESSAGE_CREATE", new JsonObject
        {
            ["id"] = messageId.ToString(),
            ["channel_id"] = this.Channel.Id.ToString(),
            ["guild_id"] = this.Guild.Id.ToString(),
            ["author"] = this._webhook.ToJson(),
            ["webhook_id"] = this._webhook.Id.ToString(),
            ["content"] = content,
            ["timestamp"] = at.ToString("O"),
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
        });
        return await this.WaitUntilSavedAsync(messageId);
    }

    private async Task<ulong> WaitUntilSavedAsync(ulong messageId)
    {
        (await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId), Token);
        }, saved => saved)).ShouldBeTrue();
        return messageId;
    }

    private async Task<int> LinkCountAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return await dbContext.ProxiedMessages.CountAsync(Token);
    }

    private Task<List<EarnedXp>> WaitForXpAsync(FakeUser user, int count, TimeSpan? window = null)
        => GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.XpHistory.OfType<EarnedXp>()
                .Where(xp => xp.UserId == new UserId(user.Id))
                .ToListAsync(Token);
        }, earned => earned.Count >= count, window);

    private bool IsPostInChannel(RecordedRequest request)
        => request.Method == HttpMethod.Post && request.Path == $"channels/{this.Channel.Id}/messages";

    private Task<bool> DeleteLoggedWithinAsync(TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                               && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString()
                                                                   .StartsWith("Message deleted", StringComparison.Ordinal) == true, window);

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

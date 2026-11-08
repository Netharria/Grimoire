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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>
///     PluralKit proxies in the message log (backlog A-34). PluralKit deletes a member's message and reposts it
///     through a webhook. The bot asks PluralKit about deleted messages under a minute old. When the deleted one was
///     an original, it links it to the proxy instead of logging a delete. A later delete of the proxy is logged with
///     the original author and the system and member ids. Leveling and the spam filter don't use PluralKit.
/// </summary>
[Collection("Test collection")]
public sealed class PluralKitTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private static readonly TimeSpan NothingHappensWindow = TimeSpan.FromSeconds(2);

    private readonly GrimoireCoreFactory _database = database;
    private readonly FakePluralKit _pluralKit = new();
    private readonly FakeUser _webhook = new(1_100_000_000_000_006_001, "Alice", IsBot: true);
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;
    private FakeChannel Channel => this._bot.Guild.General;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database, configureHost: builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["pluralkitToken"] = "fake-token" });
            builder.Services.AddHttpClient("Pluralkit").ConfigurePrimaryHttpMessageHandler(() => this._pluralKit);
        });
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureMessageLogAsync();
        (await this._bot.Services.GetRequiredService<SettingsModule>().SetLogChannelSetting(GuildLogType.MessageDeleted,
            new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), new ChannelId(this.Guild.ModLog.Id))).ShouldBeSetOrUnchanged();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>When PluralKit deletes the original it proxied, that's linked, not logged as a delete.</summary>
    [Fact]
    public async Task TheOriginalBeingReplacedByAProxy_IsLinked_NotLogged()
    {
        var (original, proxy) = await this.ProxyAsync("hello");

        this._bot.DeleteMessage(original, this.Channel);

        (await GrimoireTestBot.EventuallyAsync(this.LinksAsync, links => links.Count > 0)).ShouldHaveSingleItem()
            .ShouldBe((proxy, original, "abcde", "fghij"));
        (await this.DeleteLoggedWithinAsync(NothingHappensWindow)).ShouldBeFalse();
        this._pluralKit.Lookups.ShouldContain($"messages/{original}");
        this._bot.Logs.Errors.ShouldBeEmpty();
    }

    /// <summary>Deleting the proxy afterwards is logged against the member who wrote it.</summary>
    [Fact]
    public async Task DeletingTheProxy_IsLoggedWithTheOriginalAuthorAndSystem()
    {
        var (original, proxy) = await this.ProxyAsync("hello");
        this._bot.DeleteMessage(original, this.Channel);
        await GrimoireTestBot.EventuallyAsync(this.LinksAsync, links => links.Count > 0);

        this._bot.DeleteMessage(proxy, this.Channel);

        var embed = await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == $"Message deleted in #{this.Channel.Name}");
        Field(embed, "Original Author").ShouldBe(this.Guild.User1.Mention);
        Field(embed, "System Id").ShouldBe("abcde");
        Field(embed, "Member Id").ShouldBe("fghij");
        Field(embed, "**Content**").ShouldBe("hello");
    }

    /// <summary>A message PluralKit doesn't know is deleted and logged as usual.</summary>
    [Fact]
    public async Task AMessagePluralKitDoesNotKnow_IsLoggedAsUsual()
    {
        var messageId = await this.SendLoggedAsync(this.Guild.User1, "plain");

        this._bot.DeleteMessage(messageId, this.Channel);

        Field(await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == $"Message deleted in #{this.Channel.Name}"),
            "Author").ShouldBe(this.Guild.User1.Mention);
        (await this.LinksAsync()).ShouldBeEmpty();
    }

    /// <summary>
    ///     I-3: the lookup is optional, so a PluralKit failure must not cost the delete log. A refused token, a reply
    ///     that isn't JSON, and one whose shape changed each make it fail differently.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "")]
    [InlineData(HttpStatusCode.OK, "<html>Service unavailable</html>")]
    [InlineData(HttpStatusCode.OK, """{"id": 12345, "original": ["not", "a", "string"]}""")]
    public async Task APluralKitFailure_StillLogsTheDelete(HttpStatusCode status, string body)
    {
        var messageId = await this.SendLoggedAsync(this.Guild.User1, "plain");
        this._pluralKit.FailWith(status, body);

        this._bot.DeleteMessage(messageId, this.Channel);

        Field(await this._bot.WaitForModLogEmbedAsync(e => e["author"]?["name"]?.GetValue<string>() == $"Message deleted in #{this.Channel.Name}"),
            "Author").ShouldBe(this.Guild.User1.Mention);
    }

    /// <summary>I-3: the rate limiter allows two lookups a second with no queue, so a burst of deletes is refused by it.</summary>
    [Fact]
    public async Task ABurstOfDeletes_IsAllLogged()
    {
        ulong[] messageIds = [.. await Task.WhenAll(Enumerable.Range(0, 5).Select(i => this.SendLoggedAsync(this.Guild.User1, $"plain {i}")))];

        foreach (var messageId in messageIds)
            this._bot.DeleteMessage(messageId, this.Channel);

        var logged = await GrimoireTestBot.EventuallyAsync(() => Task.FromResult(this._bot.Rest.Requests.Count(request =>
                this._bot.IsModLogPost(request) && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString()
                    .StartsWith("Message deleted", StringComparison.Ordinal) == true)),
            count => count >= messageIds.Length);
        logged.ShouldBe(messageIds.Length);
    }

    /// <summary>I-15: without PluralKit configured, the bot says so once, not on every delete.</summary>
    [Fact]
    public async Task WithoutPluralKitConfigured_TheWarningIsLoggedOnce()
    {
        await this._bot.DisposeAsync();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
        await this._bot.ConfigureModerationAsync();
        await this._bot.ConfigureMessageLogAsync();
        (await this._bot.Services.GetRequiredService<SettingsModule>().SetLogChannelSetting(GuildLogType.MessageDeleted,
            new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), new ChannelId(this.Guild.ModLog.Id))).ShouldBeSetOrUnchanged();

        foreach (var content in new[] { "first", "second", "third" })
        {
            this._bot.DeleteMessage(await this.SendLoggedAsync(this.Guild.User1, content), this.Channel);
            await this._bot.WaitForModLogEmbedAsync(e => e["fields"]?.AsArray()
                .Any(field => field?["value"]?.GetValue<string>() == content) == true);
        }

        this._bot.Logs.Events.Count(logEvent => logEvent.MessageTemplate.Text.Contains("Pluralkit integration disabled", StringComparison.Ordinal))
            .ShouldBe(1);
    }

    /// <summary>The original, then the proxy PluralKit posts in its place, both saved by the message log.</summary>
    private async Task<(ulong Original, ulong Proxy)> ProxyAsync(string content)
    {
        var original = await this.SendLoggedAsync(this.Guild.User1, $"a:{content}");
        var proxy = await this.SendLoggedAsync(this._webhook, content);
        this._pluralKit.AddProxy(original, proxy, this.Guild.User1, this.Guild, "abcde", "fghij");
        return (original, proxy);
    }

    private async Task<ulong> SendLoggedAsync(FakeUser author, string content)
    {
        var messageId = this._bot.SendMessage(author, this.Channel, content);
        (await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.Messages.AnyAsync(message => message.Id == new MessageId(messageId));
        }, saved => saved)).ShouldBeTrue();
        return messageId;
    }

    private async Task<List<(ulong, ulong, string, string)>> LinksAsync()
    {
        await using var dbContext = this._database.CreateDbContext();
        return [.. (await dbContext.ProxiedMessages.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken))
            .Select(link => (link.ProxyMessageId.Value, link.OriginalMessageId.Value, link.SystemId.Value, link.MemberId.Value))];
    }

    private Task<bool> DeleteLoggedWithinAsync(TimeSpan window)
        => this._bot.Rest.ReceivesRequestWithinAsync(request => this._bot.IsModLogPost(request)
                                                               && request.Json?["embeds"]?[0]?["author"]?["name"]?.ToString()
                                                                   .StartsWith("Message deleted", StringComparison.Ordinal) == true, window);

    private static string Field(JsonNode embed, string name)
        => embed["fields"]!.AsArray().Single(field => field!["name"]!.GetValue<string>() == name)!["value"]!.GetValue<string>();
}

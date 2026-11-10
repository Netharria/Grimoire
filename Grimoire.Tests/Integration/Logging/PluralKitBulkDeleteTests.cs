// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Grimoire.Tests.Integration.FakeDiscord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Logging;

/// <summary>
///     PluralKit proxies in the bulk delete log (backlog A-44, XC-45). The single delete and edit logs name the member
///     behind a proxy; the bulk delete dump should too.
/// </summary>
[Collection("Test collection")]
public sealed class PluralKitBulkDeleteTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private readonly FakePluralKit _pluralKit = new();
    private readonly FakeUser _webhook = new(1_100_000_000_000_006_101, "Alice", IsBot: true);
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
        (await this._bot.Services.GetRequiredService<SettingsModule>().SetLogChannelSetting(GuildLogType.BulkMessageDeleted,
            new GuildId(this.Guild.Id), new ModeratorId(this.Guild.Owner.Id), new ChannelId(this.Guild.ModLog.Id))).ShouldBeSetOrUnchanged();
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    /// <summary>XC-45: purged proxies are listed under the member who wrote them, not the webhook.</summary>
    [Fact(Skip = "Fails: the bulk delete dump names proxies by the webhook id (\"Unknown User\"), because it doesn't read ProxiedMessageLink. KI-104")]
    public async Task BulkDeletingProxies_NamesTheMemberBehindThem()
    {
        var proxies = new List<ulong>();
        foreach (var content in new[] { "one", "two", "three" })
            proxies.Add(await this.ProxyAsync(content));

        this._bot.BulkDeleteMessages(this.Channel, [.. proxies]);

        var post = await this._bot.Rest.WaitForRequestAsync(request => this._bot.IsModLogPost(request)
                                                                      && request.Json?["embeds"]?[0]?["title"]?.GetValue<string>() == "Bulk Message Delete");
        post.Json!["embeds"]![0]!["description"]!.GetValue<string>().ShouldStartWith("**Message Count:** 3\n");
        var dump = post.Files.ShouldHaveSingleItem().Value;
        foreach (var proxy in proxies)
            dump.ShouldContain($"({this.Guild.User1.Id})\nId: {proxy}\n");
        dump.ShouldNotContain(this._webhook.Id.ToString());
    }

    /// <summary>The original, replaced by a proxy and linked to it, as when PluralKit deletes the original. Returns the proxy.</summary>
    private async Task<ulong> ProxyAsync(string content)
    {
        var original = await this.SendLoggedAsync(this.Guild.User1, $"a:{content}");
        var proxy = await this.SendLoggedAsync(this._webhook, content);
        this._pluralKit.AddProxy(original, proxy, this.Guild.User1, this.Guild, "abcde", "fghij");
        this._bot.DeleteMessage(original, this.Channel);
        (await GrimoireTestBot.EventuallyAsync(async () =>
        {
            await using var dbContext = this._database.CreateDbContext();
            return await dbContext.ProxiedMessages.AnyAsync(link => link.ProxyMessageId == new MessageId(proxy));
        }, linked => linked)).ShouldBeTrue("the proxy should be linked to its original");
        // The PluralKit rate limiter allows two lookups a second and refuses the rest.
        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        return proxy;
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
}

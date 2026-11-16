// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Grimoire.IntegrationTests.FakeDiscord;

/// <summary>
///     Stands in for the PluralKit API behind the bot's "Pluralkit" HttpClient. <c>GET messages/{id}</c> answers for
///     either the original or the proxy id of a registered proxy, as PluralKit does, and otherwise 404 with PluralKit's
///     code 20006 ("Message not found."), which the bot reads as "not a proxy" (spec 1.5, section 3.4).
/// </summary>
public sealed class FakePluralKit : HttpMessageHandler
{
    private readonly ConcurrentQueue<DateTimeOffset> _arrivals = new();
    private readonly ConcurrentQueue<string> _lookups = new();
    private readonly ConcurrentDictionary<ulong, JsonObject> _messages = new();
    private (HttpStatusCode Status, string Body)? _failure;

    public IReadOnlyCollection<string> Lookups => this._lookups;

    /// <summary>How long each request takes to answer, to fill the bot's rate limiter queue.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>The headers of the last request, to check the user agent and token.</summary>
    public HttpRequestHeaders? LastHeaders { get; private set; }

    /// <summary>The most requests that arrived within any one second, to check the bot's rate limiter.</summary>
    public int MostInOneSecond
    {
        get
        {
            var arrivals = this._arrivals.Order().ToArray();
            return arrivals.Select((start, i) => arrivals.Skip(i).Count(at => at - start < TimeSpan.FromSeconds(1)))
                .DefaultIfEmpty(0).Max();
        }
    }

    /// <summary>Registers a proxied message: the member's original, the webhook's proxy, and the system and member ids.</summary>
    public void AddProxy(ulong originalId, ulong proxyId, FakeUser sender, FakeGuild guild, string systemId, string memberId)
    {
        var json = Message(originalId, proxyId, sender, guild);
        json["system"] = new JsonObject { ["id"] = systemId, ["uuid"] = Guid.NewGuid().ToString(), ["privacy"] = new JsonObject() };
        json["member"] = new JsonObject
        {
            ["id"] = memberId,
            ["uuid"] = Guid.NewGuid().ToString(),
            ["name"] = "Alice",
            ["keep_proxy"] = false,
            ["tts"] = false,
            ["privacy"] = new JsonObject()
        };
        this._messages[originalId] = json;
        this._messages[proxyId] = json;
    }

    /// <summary>A proxy whose member was deleted: PluralKit sends <c>system</c> and <c>member</c> as null (Q37).</summary>
    public void AddProxyOfDeletedMember(ulong originalId, ulong proxyId, FakeUser sender, FakeGuild guild)
    {
        var json = Message(originalId, proxyId, sender, guild);
        json["system"] = null;
        json["member"] = null;
        this._messages[originalId] = json;
        this._messages[proxyId] = json;
    }

    /// <summary>Every lookup gets <paramref name="status" /> with <paramref name="body" />, as when PluralKit is failing.</summary>
    public void FailWith(HttpStatusCode status, string body = "")
        => this._failure = (status, body);

    /// <summary>Every lookup gets a 429 with PluralKit's body and <c>retry_after</c> in milliseconds.</summary>
    public void RateLimitWith(int retryAfterMs)
        => this._failure = (HttpStatusCode.TooManyRequests,
            $$"""{"message": "429: too many requests", "retry_after": {{retryAfterMs}}, "code": 0}""");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.Trim('/');
        this._lookups.Enqueue(path);
        this._arrivals.Enqueue(DateTimeOffset.UtcNow);
        this.LastHeaders = request.Headers;
        if (this.Delay > TimeSpan.Zero)
            await Task.Delay(this.Delay, cancellationToken);
        if (this._failure is var (status, body))
            return Json(status, body);
        // The path is under the configured endpoint: "messages/{id}", or "v2/messages/{id}".
        var at = path.LastIndexOf("messages/", StringComparison.Ordinal);
        return at >= 0
               && ulong.TryParse(path[(at + "messages/".Length)..], out var id)
               && this._messages.TryGetValue(id, out var json)
            ? Json(HttpStatusCode.OK, json.ToJsonString())
            : Json(HttpStatusCode.NotFound, """{"message": "Message not found.", "code": 20006}""");
    }

    private static JsonObject Message(ulong originalId, ulong proxyId, FakeUser sender, FakeGuild guild) => new()
    {
        ["timestamp"] = DateTime.UtcNow.ToString("O"),
        ["id"] = proxyId.ToString(),
        ["original"] = originalId.ToString(),
        ["sender"] = sender.Id.ToString(),
        ["channel"] = guild.General.Id.ToString(),
        ["guild"] = guild.Id.ToString()
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

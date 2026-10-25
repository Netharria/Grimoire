// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Grimoire.Tests.Integration.FakeDiscord;

/// <summary>
///     Stands in for the PluralKit API behind the bot's "Pluralkit" HttpClient. <c>GET messages/{id}</c> answers for
///     either the original or the proxy id of a registered proxy, as PluralKit does, and 404 otherwise.
/// </summary>
public sealed class FakePluralKit : HttpMessageHandler
{
    private readonly ConcurrentDictionary<ulong, JsonObject> _messages = new();
    private readonly ConcurrentQueue<string> _lookups = new();

    public IReadOnlyCollection<string> Lookups => this._lookups;

    /// <summary>Registers a proxied message: the member's original, the webhook's proxy, and the system and member ids.</summary>
    public void AddProxy(ulong originalId, ulong proxyId, FakeUser sender, FakeGuild guild, string systemId, string memberId)
    {
        var json = new JsonObject
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["id"] = proxyId.ToString(),
            ["original"] = originalId.ToString(),
            ["sender"] = sender.Id.ToString(),
            ["channel"] = guild.General.Id.ToString(),
            ["guild"] = guild.Id.ToString(),
            ["system"] = new JsonObject { ["id"] = systemId, ["uuid"] = Guid.NewGuid().ToString(), ["privacy"] = new JsonObject() },
            ["member"] = new JsonObject
            {
                ["id"] = memberId,
                ["uuid"] = Guid.NewGuid().ToString(),
                ["name"] = "Alice",
                ["keep_proxy"] = false,
                ["tts"] = false,
                ["privacy"] = new JsonObject()
            }
        };
        this._messages[originalId] = json;
        this._messages[proxyId] = json;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.Trim('/');
        this._lookups.Enqueue(path);
        return Task.FromResult(path.StartsWith("messages/", StringComparison.Ordinal)
                               && ulong.TryParse(path["messages/".Length..], out var id)
                               && this._messages.TryGetValue(id, out var json)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

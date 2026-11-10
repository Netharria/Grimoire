// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Grimoire.Tests.Integration.FakeDiscord;

/// <summary>
///     H-11: the messages an interaction's webhook owns (its original response and follow-ups), kept so that a test
///     can read their components and click one. Discord keeps one id per message, so editing the original response
///     changes that message rather than making a new one. A component interaction's own <c>@original</c> is the
///     message the component sits on.
/// </summary>
public sealed partial class FakeDiscordRest
{
    private const int ChannelMessageCallback = 4;
    private const int DeferredChannelMessageCallback = 5;
    private const int UpdateMessageCallback = 7;

    private readonly Dictionary<ulong, JsonObject> _webhookMessages = [];
    private readonly Dictionary<string, ulong> _originalOfToken = [];
    private readonly Dictionary<string, ulong> _channelOfToken = [];
    private readonly Dictionary<string, JsonNode?> _deferredFlagsOfToken = [];

    /// <summary>The channel an interaction was sent from, so its responses are posted there.</summary>
    public void RememberInteractionChannel(string token, ulong channelId)
    {
        lock (this._lock)
            this._channelOfToken[token] = channelId;
    }

    /// <summary>A component interaction's <c>@original</c> is the message holding the component that was used.</summary>
    public void RememberComponentTarget(string token, ulong messageId, ulong channelId)
    {
        lock (this._lock)
        {
            this._originalOfToken[token] = messageId;
            this._channelOfToken[token] = channelId;
        }
    }

    /// <summary>
    ///     The interaction's original response as the member now sees it, with its components, or null if there is none
    ///     yet or it was deleted.
    /// </summary>
    public JsonObject? OriginalResponseOf(string token)
    {
        lock (this._lock)
            return this._originalOfToken.TryGetValue(token, out var id) && this._webhookMessages.TryGetValue(id, out var message)
                ? (JsonObject)message.DeepClone()
                : null;
    }

    /// <summary>A webhook message by id, such as a follow-up, as it stands now; null once deleted.</summary>
    public JsonObject? WebhookMessage(ulong messageId)
    {
        lock (this._lock)
            return this._webhookMessages.TryGetValue(messageId, out var message) ? (JsonObject)message.DeepClone() : null;
    }

    private HttpResponseMessage InteractionCallbackResponse(RecordedRequest request)
    {
        var token = InteractionCallbackToken().Match(request.Path).Groups[1].Value;
        var callback = request.Json;
        var type = callback?["type"]?.GetValue<int>();
        var data = callback?["data"] as JsonObject;
        lock (this._lock)
        {
            if (type == DeferredChannelMessageCallback)
                // The defer decides whether the response is ephemeral; the later edit doesn't repeat the flag.
                this._deferredFlagsOfToken[token] = data?["flags"]?.DeepClone();
            else if (type == ChannelMessageCallback)
                this._originalOfToken[token] = this.StoreWebhookMessage(token, data);
            else if (type == UpdateMessageCallback && this._originalOfToken.TryGetValue(token, out var id)
                                                   && this._webhookMessages.TryGetValue(id, out var message))
                ApplyEdit(message, data);
        }

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage WebhookMessageResponse(RecordedRequest request)
    {
        var match = WebhookMessagePath().Match(request.Path);
        var token = match.Groups[1].Value;
        var which = match.Groups[2].Value;
        lock (this._lock)
        {
            ulong? id = which == "@original"
                ? this._originalOfToken.TryGetValue(token, out var original) ? original : null
                : ulong.Parse(which);
            var stored = id is { } known && this._webhookMessages.TryGetValue(known, out var message) ? message : null;

            if (request.Method == HttpMethod.Patch)
            {
                // A deferred response has no message until its first edit.
                if (stored is null && which == "@original")
                {
                    this._originalOfToken[token] = this.StoreWebhookMessage(token, request.Json as JsonObject);
                    return Json(this._webhookMessages[this._originalOfToken[token]].DeepClone());
                }

                if (stored is null)
                    return Error(HttpStatusCode.NotFound, 10008, "Unknown Message");
                ApplyEdit(stored, request.Json as JsonObject);
                return Json(stored.DeepClone());
            }

            if (request.Method == HttpMethod.Delete)
            {
                // Deleting a deferred response that never got its message is accepted, as it always was here.
                if (stored is null && which != "@original")
                    return Error(HttpStatusCode.NotFound, 10008, "Unknown Message");
                if (id is { } deleted)
                    this._webhookMessages.Remove(deleted);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return stored is null ? Error(HttpStatusCode.NotFound, 10008, "Unknown Message") : Json(stored.DeepClone());
        }
    }

    private HttpResponseMessage FollowupResponse(RecordedRequest request)
    {
        var token = FollowupMessage().Match(request.Path).Value.Split('/')[2];
        lock (this._lock)
            return Json(this._webhookMessages[this.StoreWebhookMessage(token, request.Json as JsonObject)].DeepClone());
    }

    /// <summary>Call under <c>_lock</c>.</summary>
    private ulong StoreWebhookMessage(string token, JsonObject? body)
    {
        var channelId = this._channelOfToken.TryGetValue(token, out var channel) ? channel : this._guild.General.Id;
        var message = this.MessageJson(channelId, body);
        message["flags"] = this._deferredFlagsOfToken.Remove(token, out var deferredFlags) && deferredFlags is not null
            ? deferredFlags
            : body?["flags"]?.DeepClone() ?? 0;
        var id = ulong.Parse(message["id"]!.ToString());
        this._webhookMessages[id] = message;
        return id;
    }

    private static void ApplyEdit(JsonObject message, JsonObject? body)
    {
        foreach (var field in new[] { "content", "embeds", "components" })
            if (body?[field] is { } value)
                message[field] = value.DeepClone();
        message["edited_timestamp"] = DateTimeOffset.UtcNow.ToString("O");
    }

    [GeneratedRegex(@"^interactions/\d+/([^/]+)/callback$")]
    private static partial Regex InteractionCallbackToken();

    [GeneratedRegex(@"^webhooks/\d+/([^/]+)/messages/(@original|\d+)$")]
    private static partial Regex WebhookMessagePath();
}

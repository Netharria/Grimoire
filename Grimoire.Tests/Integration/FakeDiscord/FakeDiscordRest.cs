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
using System.Text.RegularExpressions;

namespace Grimoire.Tests.Integration.FakeDiscord;

/// <summary>
///     One REST call the bot made, with the path relative to <c>/api/v10/</c>. <paramref name="AuditLogReason" /> is
///     the decoded <c>X-Audit-Log-Reason</c> header, which Discord shows in the audit log.
/// </summary>
public sealed record RecordedRequest(HttpMethod Method, string Path, string? Body, string? AuditLogReason = null)
{
    public JsonNode? Json => this.Body is null ? null : JsonNode.Parse(this.Body);

    public override string ToString() => $"{this.Method} {this.Path}";
}

/// <summary>
///     Replaces the HTTP handler under DSharpPlus's REST client. It answers the calls the bot makes at startup,
///     echoes created messages back as Discord would, records every request for assertions, and answers anything
///     it does not know with a 404 so unexpected calls show up in <see cref="Unhandled" />.
/// </summary>
public sealed partial class FakeDiscordRest(FakeGuild guild) : HttpMessageHandler
{
    private const string ApiPrefix = "/api/v10/";

    private readonly ConcurrentDictionary<string, ulong> _commandIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly FakeGuild _guild = guild;
    private readonly Lock _lock = new();
    private readonly List<RecordedRequest> _requests = [];
    private readonly List<RecordedRequest> _unhandled = [];
    private readonly List<(Func<RecordedRequest, bool> Match, TaskCompletionSource<RecordedRequest> Done)> _waiters = [];
    private long _nextId = 1_200_000_000_000_000_000;

    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (this._lock)
                return [.. this._requests];
        }
    }

    public IReadOnlyList<RecordedRequest> Unhandled
    {
        get
        {
            lock (this._lock)
                return [.. this._unhandled];
        }
    }

    /// <summary>The bodies the bot sent when it registered each slash command, keyed by command name.</summary>
    public ConcurrentDictionary<string, JsonObject> RegisteredCommands { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ulong GetCommandId(string name)
        => this._commandIds.TryGetValue(name, out var id)
            ? id
            : throw new InvalidOperationException(
                $"Slash command '{name}' was not registered. Registered: {string.Join(", ", this._commandIds.Keys)}");

    /// <summary>Waits until the bot makes a request that matches, including one it already made.</summary>
    public async Task<RecordedRequest> WaitForRequestAsync(Func<RecordedRequest, bool> match, TimeSpan? timeout = null)
    {
        TaskCompletionSource<RecordedRequest> done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (this._lock)
        {
            if (this._requests.FirstOrDefault(match) is { } existing)
                return existing;
            this._waiters.Add((match, done));
        }

        try
        {
            return await done.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            lock (this._lock)
                this._waiters.RemoveAll(waiter => waiter.Done == done);
            throw new TimeoutException(
                $"No matching REST request. Requests so far:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", this.Requests)}");
        }
    }

    /// <summary>Asserts that no matching request arrives within <paramref name="window" />.</summary>
    public async Task<bool> ReceivesRequestWithinAsync(Func<RecordedRequest, bool> match, TimeSpan window)
    {
        try
        {
            await this.WaitForRequestAsync(match, window);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var absolutePath = request.RequestUri!.AbsolutePath;
        var path = absolutePath.StartsWith(ApiPrefix, StringComparison.Ordinal)
            ? absolutePath[ApiPrefix.Length..]
            : absolutePath.TrimStart('/');
        var auditLogReason = request.Headers.TryGetValues("X-Audit-Log-Reason", out var reasons)
            ? Uri.UnescapeDataString(reasons.First())
            : null;
        var recorded = new RecordedRequest(request.Method, Uri.UnescapeDataString(path),
            await ReadBodyAsync(request.Content, cancellationToken), auditLogReason);

        var response = this.Respond(recorded);

        lock (this._lock)
        {
            this._requests.Add(recorded);
            if (response is null)
                this._unhandled.Add(recorded);
            foreach (var waiter in this._waiters.Where(waiter => waiter.Match(recorded)).ToArray())
            {
                this._waiters.Remove(waiter);
                waiter.Done.TrySetResult(recorded);
            }
        }

        return response ?? Json(HttpStatusCode.NotFound,
            new JsonObject { ["message"] = $"Fake Discord has no route for {recorded}", ["code"] = 0 });
    }

    private HttpResponseMessage? Respond(RecordedRequest request)
    {
        var get = request.Method == HttpMethod.Get;
        var post = request.Method == HttpMethod.Post;

        return request.Path switch
        {
            "users/@me" when get => Json(this._guild.Bot.ToJson()),
            "oauth2/applications/@me" when get => Json(this.ApplicationJson()),
            "voice/regions" when get => Json(new JsonArray()),
            "gateway/bot" when get => Json(GatewayJson()),
            "users/@me/channels" when post => Json(this.DirectMessageChannelJson(request)),
            _ when GlobalCommands().IsMatch(request.Path) => request.Method.Method switch
            {
                "GET" => Json(new JsonArray()),
                "POST" => Json(this.RegisterCommand(request.Json!.AsObject())),
                "PUT" => Json(new JsonArray([.. request.Json!.AsArray().Select(c => (JsonNode)this.RegisterCommand(c!.AsObject()))])),
                _ => null
            },
            _ when GuildInvites().IsMatch(request.Path) && get => Json(new JsonArray()),
            _ when InteractionCallback().IsMatch(request.Path) && post => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ when OriginalResponse().IsMatch(request.Path) && request.Method == HttpMethod.Patch
                => Json(this.MessageJson(this._guild.General.Id, request)),
            _ when FollowupMessage().IsMatch(request.Path) && post
                => Json(this.MessageJson(this._guild.General.Id, request)),
            _ when (Message().IsMatch(request.Path) || Reaction().IsMatch(request.Path))
                   && request.Method == HttpMethod.Delete => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ when ChannelMessages().Match(request.Path) is { Success: true } match && post
                => Json(this.MessageJson(ulong.Parse(match.Groups[1].Value), request)),
            _ when Channel().Match(request.Path) is { Success: true } match && get
                => this._guild.Channels.Concat(this._guild.Threads)
                    .FirstOrDefault(c => c.Id == ulong.Parse(match.Groups[1].Value)) is { } channel
                    ? Json(this._guild.ChannelJson(channel))
                    : null,
            _ => null
        };
    }

    private JsonObject RegisterCommand(JsonObject command)
    {
        var name = command["name"]!.GetValue<string>();
        var id = this._commandIds.GetOrAdd(name, StableCommandId);
        this.RegisteredCommands[name] = command.DeepClone().AsObject();

        var created = command.DeepClone().AsObject();
        created["id"] = FakeUser.Snowflake(id);
        created["application_id"] = FakeUser.Snowflake(this._guild.ApplicationId);
        created["version"] = FakeUser.Snowflake(id);
        return created;
    }

    /// <summary>
    ///     DSharpPlus keeps its command-id map in a static field, so ids must not change between test hosts in
    ///     the same process. Deriving them from the name keeps every host's map identical.
    /// </summary>
    private static ulong StableCommandId(string name)
    {
        var hash = 14_695_981_039_346_656_037UL;
        foreach (var character in name.ToLowerInvariant())
            hash = (hash ^ character) * 1_099_511_628_211UL;
        return 1_300_000_000_000_000_000 + hash % 100_000_000_000_000_000;
    }

    private JsonObject MessageJson(ulong channelId, RecordedRequest request)
    {
        var body = request.Json as JsonObject;
        return new JsonObject
        {
            ["id"] = FakeUser.Snowflake(this.NextId()),
            ["channel_id"] = FakeUser.Snowflake(channelId),
            ["author"] = this._guild.Bot.ToJson(),
            ["content"] = body?["content"]?.DeepClone() ?? "",
            ["embeds"] = body?["embeds"]?.DeepClone() ?? new JsonArray(),
            ["components"] = new JsonArray(),
            ["attachments"] = new JsonArray(),
            ["mentions"] = new JsonArray(),
            ["mention_roles"] = new JsonArray(),
            ["mention_everyone"] = false,
            ["tts"] = false,
            ["pinned"] = false,
            ["type"] = 0,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["edited_timestamp"] = null
        };
    }

    private JsonObject DirectMessageChannelJson(RecordedRequest request)
    {
        var recipientId = ulong.Parse(request.Json!["recipient_id"]!.ToString());
        var recipient = this._guild.Members.Select(member => member.User).Single(user => user.Id == recipientId);
        return new JsonObject
        {
            ["id"] = FakeUser.Snowflake(recipientId + 1_000),
            ["type"] = 1,
            ["last_message_id"] = null,
            ["recipients"] = new JsonArray(recipient.ToJson())
        };
    }

    private JsonObject ApplicationJson() => new()
    {
        ["id"] = FakeUser.Snowflake(this._guild.ApplicationId),
        ["name"] = this._guild.Bot.Username,
        ["icon"] = null,
        ["description"] = "",
        ["bot_public"] = false,
        ["bot_require_code_grant"] = false,
        ["verify_key"] = "fake",
        ["flags"] = 0,
        ["owner"] = this._guild.Owner.ToJson(),
        ["team"] = null
    };

    private static JsonObject GatewayJson() => new()
    {
        ["url"] = "wss://gateway.fake.discord.invalid",
        ["shards"] = 1,
        ["session_start_limit"] = new JsonObject
        {
            ["total"] = 1000,
            ["remaining"] = 1000,
            ["reset_after"] = 0,
            ["max_concurrency"] = 1
        }
    };

    private ulong NextId() => (ulong)Interlocked.Increment(ref this._nextId);

    private static async Task<string?> ReadBodyAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is MultipartFormDataContent multipart)
        {
            var payloadPart = multipart.FirstOrDefault(part =>
                part.Headers.ContentDisposition?.Name?.Trim('"') == "payload_json");
            return payloadPart is null ? null : await payloadPart.ReadAsStringAsync(cancellationToken);
        }

        if (content is null)
            return null;
        var body = await content.ReadAsStringAsync(cancellationToken);
        return body.Length == 0 ? null : body;
    }

    private static HttpResponseMessage Json(JsonNode body) => Json(HttpStatusCode.OK, body);

    private static HttpResponseMessage Json(HttpStatusCode status, JsonNode body)
        => new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    [GeneratedRegex(@"^applications/\d+(/guilds/\d+)?/commands$")]
    private static partial Regex GlobalCommands();

    [GeneratedRegex(@"^guilds/\d+/invites$")]
    private static partial Regex GuildInvites();

    [GeneratedRegex(@"^interactions/\d+/[^/]+/callback$")]
    private static partial Regex InteractionCallback();

    [GeneratedRegex(@"^webhooks/\d+/[^/]+/messages/@original$")]
    private static partial Regex OriginalResponse();

    [GeneratedRegex(@"^webhooks/\d+/[^/]+$")]
    private static partial Regex FollowupMessage();

    [GeneratedRegex(@"^channels/(\d+)/messages$")]
    private static partial Regex ChannelMessages();

    [GeneratedRegex(@"^channels/(\d+)$")]
    private static partial Regex Channel();

    [GeneratedRegex(@"^channels/\d+/messages/\d+$")]
    private static partial Regex Message();

    /// <summary>Deleting one member's reaction: <c>.../reactions/{emoji}/{userId}</c>.</summary>
    [GeneratedRegex(@"^channels/\d+/messages/\d+/reactions/[^/]+/\d+$")]
    private static partial Regex Reaction();
}

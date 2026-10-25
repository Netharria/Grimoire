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
public sealed record RecordedRequest(
    HttpMethod Method,
    string Path,
    string? Body,
    string? AuditLogReason = null,
    string Query = "",
    IReadOnlyDictionary<string, string>? Attachments = null)
{
    /// <summary>Files uploaded with the request, by file name, as text.</summary>
    public IReadOnlyDictionary<string, string> Files => this.Attachments ?? new Dictionary<string, string>();

    /// <summary>One query-string value, such as <c>before</c> on a message history page.</summary>
    public string? QueryValue(string name)
        => System.Web.HttpUtility.ParseQueryString(this.Query)[name];

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
    private readonly Dictionary<ulong, SortedDictionary<ulong, JsonObject>> _channelHistory = [];
    private readonly List<(HttpMethod Method, Regex Path, Func<RecordedRequest, HttpResponseMessage> Respond)> _overrides = [];
    private readonly HashSet<ulong> _banned = [];
    private readonly List<JsonObject> _auditLog = [];
    private readonly Dictionary<ulong, FakeUser> _knownUsers = [];

    private readonly HashSet<ulong> _kicked = [];

    public const int KickAuditAction = 20;
    public const int BanAuditAction = 22;
    public const int MessageDeleteAuditAction = 72;
    public const int UnbanAuditAction = 23;

    /// <summary>
    ///     Sends a gateway event, set by the test host. The fake uses it for Discord's side effects, such as the
    ///     <c>GUILD_BAN_ADD</c> that follows a ban.
    /// </summary>
    public Action<string, JsonNode>? Dispatch { get; set; }

    /// <summary>Users currently banned from the guild.</summary>
    public IReadOnlyCollection<ulong> Banned
    {
        get
        {
            lock (this._lock)
                return [.. this._banned];
        }
    }

    /// <summary>Lets the fake describe users who aren't guild members, in events and audit logs.</summary>
    public void RememberUser(FakeUser user)
    {
        lock (this._lock)
            this._knownUsers[user.Id] = user;
    }

    /// <summary>Adds an audit-log entry, dated now by its id, the way Discord dates every entry.</summary>
    public void AddAuditLogEntry(int actionType, FakeUser responsible, ulong targetId, string? reason, JsonObject? options = null)
    {
        lock (this._lock)
        {
            this._knownUsers[responsible.Id] = responsible;
            this._auditLog.Add(new JsonObject
            {
                ["id"] = FakeUser.Snowflake(SnowflakeAt(DateTimeOffset.UtcNow, this._auditLog.Count)),
                ["user_id"] = FakeUser.Snowflake(responsible.Id),
                ["target_id"] = FakeUser.Snowflake(targetId),
                ["action_type"] = actionType,
                ["reason"] = reason,
                ["changes"] = new JsonArray(),
                ["options"] = options
            });
        }
    }

    /// <summary>
    ///     Records a ban as Discord does: it enters the ban list and audit log, and the gateway announces it.
    ///     Banning someone already banned changes nothing and sends no event.
    /// </summary>
    public void ApplyBan(FakeUser moderator, ulong targetId, string? reason)
    {
        lock (this._lock)
            if (!this._banned.Add(targetId))
                return;
        this.AddAuditLogEntry(BanAuditAction, moderator, targetId, reason);
        this.Dispatch?.Invoke("GUILD_BAN_ADD", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(this._guild.Id),
            ["user"] = this.UserJson(targetId)
        });
    }

    /// <summary>Puts someone on the ban list with no event or audit entry, as if banned before the bot joined.</summary>
    public void MarkBanned(ulong userId)
    {
        lock (this._lock)
            this._banned.Add(userId);
    }

    /// <summary>Lifts a ban as Discord does. Returns false when the user wasn't banned.</summary>
    public bool ApplyUnban(FakeUser moderator, ulong targetId)
    {
        lock (this._lock)
            if (!this._banned.Remove(targetId))
                return false;
        this.AddAuditLogEntry(UnbanAuditAction, moderator, targetId, null);
        this.Dispatch?.Invoke("GUILD_BAN_REMOVE", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(this._guild.Id),
            ["user"] = this.UserJson(targetId)
        });
        return true;
    }

    private JsonObject UserJson(ulong userId)
    {
        lock (this._lock)
            return (this._guild.Members.Select(member => member.User).FirstOrDefault(user => user.Id == userId)
                    ?? this._knownUsers.GetValueOrDefault(userId)
                    ?? new FakeUser(userId, $"user-{userId}")).ToJson();
    }
    private readonly FakeGuild _primary = guild;

    // The guild the request in flight is about, picked from its path. Responses read it through _guild.
    private readonly AsyncLocal<FakeGuild?> _current = new();

    /// <summary>A second guild. Requests whose path names its id or one of its channels act on it.</summary>
    public FakeGuild? SecondGuild { get; set; }

    private FakeGuild _guild => this._current.Value ?? this._primary;

    private FakeGuild GuildForPath(string path)
    {
        if (this.SecondGuild is not { } second)
            return this._primary;
        var parts = path.Split('/');
        if (parts.Length > 1 && ulong.TryParse(parts[1], out var id)
            && (parts[0] == "guilds" ? id == second.Id
                : parts[0] == "channels" && (second.Channels.Any(c => c.Id == id) || second.Threads.Any(c => c.Id == id))))
            return second;
        return this._primary;
    }
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

    /// <summary>
    ///     Answers matching requests with <paramref name="respond" /> instead of the built-in route, for example a 403
    ///     on a DM channel. <paramref name="pathPattern" /> is a regex over the path after <c>/api/v10/</c>. The newest
    ///     override wins.
    /// </summary>
    public void On(HttpMethod method, string pathPattern, Func<RecordedRequest, HttpResponseMessage> respond)
    {
        lock (this._lock)
            this._overrides.Insert(0, (method, new Regex($"^{pathPattern}$"), respond));
    }

    /// <summary>A Discord error response, such as 403 with code 50007 "Cannot send messages to this user".</summary>
    public static HttpResponseMessage Error(HttpStatusCode status, int code, string message)
        => Json(status, new JsonObject { ["message"] = message, ["code"] = code });

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
            await ReadBodyAsync(request.Content, cancellationToken), auditLogReason, request.RequestUri.Query,
            await ReadFilesAsync(request.Content, cancellationToken));

        Func<RecordedRequest, HttpResponseMessage>? overridden;
        lock (this._lock)
            overridden = this._overrides
                .FirstOrDefault(o => o.Method == recorded.Method && o.Path.IsMatch(recorded.Path)).Respond;
        this._current.Value = this.GuildForPath(recorded.Path);
        var response = overridden is not null ? overridden(recorded) : this.Respond(recorded);

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
            _ when GuildInvites().IsMatch(request.Path) && get
                => Json(new JsonArray([.. this._guild.Invites.Select(invite => (JsonNode)this._guild.InviteJson(invite))])),
            _ when GuildBan().Match(request.Path) is { Success: true } match && request.Method == HttpMethod.Put
                => this.Ban(ulong.Parse(match.Groups[1].Value), request),
            _ when GuildBan().Match(request.Path) is { Success: true } match && request.Method == HttpMethod.Delete
                => this.ApplyUnban(this._guild.Bot, ulong.Parse(match.Groups[1].Value))
                    ? new HttpResponseMessage(HttpStatusCode.NoContent)
                    : Error(HttpStatusCode.NotFound, 10026, "Unknown Ban"),
            _ when AuditLogs().IsMatch(request.Path) && get => this.AuditLogPage(request),
            _ when GuildMember().Match(request.Path) is { Success: true } match && request.Method == HttpMethod.Delete
                => this.Kick(ulong.Parse(match.Groups[1].Value), request),
            _ when GuildMember().Match(request.Path) is { Success: true } match && request.Method == HttpMethod.Patch
                => this.ModifyMember(ulong.Parse(match.Groups[1].Value), request),
            _ when GuildMember().Match(request.Path) is { Success: true } match && get
                => this._guild.Members.FirstOrDefault(member => member.User.Id == ulong.Parse(match.Groups[1].Value)) is { } member
                    ? Json(FakeGuild.MemberJson(member))
                    : Error(HttpStatusCode.NotFound, 10007, "Unknown Member"),
            _ when GuildRoles().IsMatch(request.Path) && post => this.CreateRole(request),
            _ when MemberRole().Match(request.Path) is { Success: true } match
                   && (request.Method == HttpMethod.Put || request.Method == HttpMethod.Delete)
                => this.ChangeMemberRole(ulong.Parse(match.Groups[1].Value), ulong.Parse(match.Groups[2].Value),
                    request.Method == HttpMethod.Put),
            _ when GuildRole().Match(request.Path) is { Success: true } match && get
                => this._guild.RoleJsonOf(ulong.Parse(match.Groups[1].Value)) is { } role
                    ? Json(role)
                    : Error(HttpStatusCode.NotFound, 10011, "Unknown Role"),
            _ when ChannelPermission().Match(request.Path) is { Success: true } match && request.Method == HttpMethod.Put
                => this.SetOverwrite(ulong.Parse(match.Groups[1].Value), ulong.Parse(match.Groups[2].Value), request),
            _ when InteractionCallback().IsMatch(request.Path) && post => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ when OriginalResponse().IsMatch(request.Path) && request.Method == HttpMethod.Delete
                => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ when OriginalResponse().IsMatch(request.Path) && request.Method == HttpMethod.Patch
                => Json(this.MessageJson(this._guild.General.Id, request)),
            _ when FollowupMessage().IsMatch(request.Path) && post
                => Json(this.MessageJson(this._guild.General.Id, request)),
            _ when Message().Match(request.Path) is { Success: true } match && request.Method == HttpMethod.Delete
                => this.DeleteFromHistory(ulong.Parse(match.Groups[1].Value), [ulong.Parse(match.Groups[2].Value)]),
            _ when BulkDelete().Match(request.Path) is { Success: true } match && post
                => this.DeleteFromHistory(ulong.Parse(match.Groups[1].Value),
                    [.. request.Json!["messages"]!.AsArray().Select(id => ulong.Parse(id!.ToString()))]),
            _ when Reaction().IsMatch(request.Path) && request.Method == HttpMethod.Delete
                => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ when ChannelMessages().Match(request.Path) is { Success: true } match && get
                => Json(this.HistoryPage(ulong.Parse(match.Groups[1].Value), request)),
            _ when ChannelMessages().Match(request.Path) is { Success: true } match && post
                => this._guild.Channels.Any(channel => channel.Id == ulong.Parse(match.Groups[1].Value))
                   && !this._guild.BotCanSend(ulong.Parse(match.Groups[1].Value))
                    ? Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions")
                    : Json(this.PostToHistory(ulong.Parse(match.Groups[1].Value), request)),
            _ when Message().Match(request.Path) is { Success: true } match && get
                => this.FromHistory(ulong.Parse(match.Groups[1].Value), ulong.Parse(match.Groups[2].Value)) is { } stored
                    ? Json(stored)
                    : Error(HttpStatusCode.NotFound, 10008, "Unknown Message"),
            _ when Message().Match(request.Path) is { Success: true } match && request.Method == HttpMethod.Patch
                => this.EditInHistory(ulong.Parse(match.Groups[1].Value), ulong.Parse(match.Groups[2].Value), request) is { } edited
                    ? Json(edited)
                    : Error(HttpStatusCode.NotFound, 10008, "Unknown Message"),
            _ when Channel().Match(request.Path) is { Success: true } match && get
                => this._guild.Channels.Concat(this._guild.Threads)
                    .FirstOrDefault(c => c.Id == ulong.Parse(match.Groups[1].Value)) is { } channel
                    ? Json(this._guild.ChannelJson(channel))
                    : Error(HttpStatusCode.NotFound, 10003, "Unknown Channel"),
            _ => null
        };
    }

    /// <summary>Discord refuses a ban without Ban Members, or of anyone ranked at or above the bot.</summary>
    private HttpResponseMessage Ban(ulong targetId, RecordedRequest request)
    {
        if (!this._guild.BotHas(FakeGuild.BanMembersPermission)
            || this._guild.HierarchyOf(targetId) >= this._guild.HierarchyOf(this._guild.Bot.Id))
            return Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions");
        this.ApplyBan(this._guild.Bot, targetId, request.AuditLogReason);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    /// <summary>
    ///     Discord refuses a kick without Kick Members, or of anyone ranked at or above the bot. A kick removes the
    ///     member, which the gateway announces with <c>GUILD_MEMBER_REMOVE</c>.
    /// </summary>
    private HttpResponseMessage Kick(ulong targetId, RecordedRequest request)
    {
        if (!this._guild.BotHas(FakeGuild.KickMembersPermission)
            || this._guild.HierarchyOf(targetId) >= this._guild.HierarchyOf(this._guild.Bot.Id))
            return Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions");
        lock (this._lock)
            this._kicked.Add(targetId);
        this.AddAuditLogEntry(KickAuditAction, this._guild.Bot, targetId, request.AuditLogReason);
        this.Dispatch?.Invoke("GUILD_MEMBER_REMOVE", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(this._guild.Id),
            ["user"] = this.UserJson(targetId)
        });
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    /// <summary>Creating a role needs Manage Roles. Discord announces it with <c>GUILD_ROLE_CREATE</c>.</summary>
    private HttpResponseMessage CreateRole(RecordedRequest request)
    {
        if (!this._guild.BotHas(FakeGuild.ManageRolesPermission))
            return Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions");
        var body = request.Json as JsonObject;
        var permissions = ulong.TryParse(body?["permissions"]?.ToString(), out var parsed) ? parsed : 0;
        var roleId = this._guild.AddRole(body?["name"]?.GetValue<string>() ?? "new role", permissions);
        var role = this._guild.RoleJsonOf(roleId)!;
        this.Dispatch?.Invoke("GUILD_ROLE_CREATE", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(this._guild.Id),
            ["role"] = role.DeepClone()
        });
        return Json(role);
    }

    /// <summary>
    ///     Editing an overwrite needs Manage Roles and a channel the bot can see. Discord announces the new
    ///     overwrites with <c>CHANNEL_UPDATE</c>, which is how the bot's cache learns them.
    /// </summary>
    private HttpResponseMessage SetOverwrite(ulong channelId, ulong targetId, RecordedRequest request)
    {
        if (!this._guild.BotHas(FakeGuild.ManageRolesPermission) || !this._guild.BotCanView(channelId))
            return Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions");
        var body = request.Json!;
        this._guild.SetOverwrite(channelId, targetId, body["type"]?.GetValue<int>() ?? 0,
            ulong.Parse(body["allow"]?.ToString() ?? "0"), ulong.Parse(body["deny"]?.ToString() ?? "0"));
        if (this._guild.Channels.FirstOrDefault(channel => channel.Id == channelId) is { } updated)
            this.Dispatch?.Invoke("CHANNEL_UPDATE", this._guild.ChannelJson(updated));
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    /// <summary>
    ///     Granting or revoking a role needs Manage Roles and a role below the bot's top role. Discord announces the
    ///     member's new roles with <c>GUILD_MEMBER_UPDATE</c>.
    /// </summary>
    private HttpResponseMessage ChangeMemberRole(ulong userId, ulong roleId, bool grant)
    {
        var user = this._guild.Members.Select(member => member.User).FirstOrDefault(member => member.Id == userId);
        if (user is null)
            return Error(HttpStatusCode.NotFound, 10007, "Unknown Member");
        if (!this._guild.HasRole(roleId))
            return Error(HttpStatusCode.NotFound, 10011, "Unknown Role");
        if (!this._guild.BotCanManageRole(roleId))
            return Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions");

        if (grant)
            this._guild.GrantRole(user, roleId);
        else
            this._guild.RevokeRole(user, roleId);
        var update = FakeGuild.MemberJson(this._guild.GetMember(user));
        update["guild_id"] = FakeUser.Snowflake(this._guild.Id);
        this.Dispatch?.Invoke("GUILD_MEMBER_UPDATE", update);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    /// <summary>
    ///     Modifying a member. Only a new role list is applied: every role added or removed must be below the bot's top
    ///     role, as on Discord. The new roles are announced with <c>GUILD_MEMBER_UPDATE</c>.
    /// </summary>
    private HttpResponseMessage ModifyMember(ulong userId, RecordedRequest request)
    {
        var member = this._guild.Members.FirstOrDefault(candidate => candidate.User.Id == userId);
        if (member is null)
            return Error(HttpStatusCode.NotFound, 10007, "Unknown Member");
        if (request.Json?["roles"] is not JsonArray roles)
            return Json(FakeGuild.MemberJson(member));

        var newRoles = roles.Select(role => ulong.Parse(role!.ToString())).Where(role => role != this._guild.Id).Distinct().ToArray();
        var changed = newRoles.Except(member.RoleIds).Concat(member.RoleIds.Except(newRoles)).ToArray();
        if (changed.Any(role => !this._guild.HasRole(role)))
            return Error(HttpStatusCode.BadRequest, 50035, "Invalid Form Body");
        if (changed.Any(role => !this._guild.BotCanManageRole(role)))
            return Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions");

        this._guild.SetRoles(member.User, newRoles);
        var update = FakeGuild.MemberJson(this._guild.GetMember(member.User));
        update["guild_id"] = FakeUser.Snowflake(this._guild.Id);
        this.Dispatch?.Invoke("GUILD_MEMBER_UPDATE", update);
        return Json(FakeGuild.MemberJson(this._guild.GetMember(member.User)));
    }

    /// <summary>A moderator deletes a role from Discord's client.</summary>
    public void DeleteRole(ulong roleId)
    {
        this._guild.RemoveRole(roleId);
        this.Dispatch?.Invoke("GUILD_ROLE_DELETE", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(this._guild.Id),
            ["role_id"] = FakeUser.Snowflake(roleId)
        });
    }

    /// <summary>A moderator creates a role from Discord's client. Returns its id.</summary>
    public ulong CreateRole(string name, ulong permissions = 0, int position = 1)
    {
        var roleId = this._guild.AddRole(name, permissions, position);
        this.Dispatch?.Invoke("GUILD_ROLE_CREATE", new JsonObject
        {
            ["guild_id"] = FakeUser.Snowflake(this._guild.Id),
            ["role"] = this._guild.RoleJsonOf(roleId)!.DeepClone()
        });
        return roleId;
    }

    /// <summary>Members the bot has kicked.</summary>
    public IReadOnlyCollection<ulong> Kicked
    {
        get
        {
            lock (this._lock)
                return [.. this._kicked];
        }
    }

    private HttpResponseMessage AuditLogPage(RecordedRequest request)
    {
        if (!this._guild.BotHas(FakeGuild.ViewAuditLogPermission))
            return Error(HttpStatusCode.Forbidden, 50013, "Missing Permissions");
        var limit = int.TryParse(request.QueryValue("limit"), out var parsed) ? parsed : 50;
        var actionType = int.TryParse(request.QueryValue("action_type"), out var action) ? action : (int?)null;
        lock (this._lock)
        {
            var entries = this._auditLog
                .Where(entry => actionType is null || entry["action_type"]!.GetValue<int>() == actionType)
                .Reverse()
                .Take(limit)
                .Select(entry => (JsonNode)entry.DeepClone());
            var users = this._guild.Members.Select(member => member.User)
                .Concat(this._knownUsers.Values)
                .DistinctBy(user => user.Id)
                .Select(user => (JsonNode)user.ToJson());
            return Json(new JsonObject
            {
                ["audit_log_entries"] = new JsonArray([.. entries]),
                ["users"] = new JsonArray([.. users]),
                ["webhooks"] = new JsonArray(),
                ["integrations"] = new JsonArray(),
                ["threads"] = new JsonArray(),
                ["guild_scheduled_events"] = new JsonArray(),
                ["application_commands"] = new JsonArray(),
                ["auto_moderation_rules"] = new JsonArray()
            });
        }
    }

    /// <summary>
    ///     Puts messages in a channel's history, as if members had posted them earlier. Ids are minted from each
    ///     timestamp, so id order matches time order as it does on Discord. Returns the ids in the order given.
    /// </summary>
    public ulong[] SeedHistory(FakeChannel channel, params (FakeUser Author, DateTimeOffset At)[] messages)
    {
        lock (this._lock)
        {
            var history = this.HistoryOf(channel.Id);
            return
            [
                .. messages.Select(message =>
                {
                    var id = SnowflakeAt(message.At, history.Count);
                    history[id] = new JsonObject
                    {
                        ["id"] = FakeUser.Snowflake(id),
                        ["channel_id"] = FakeUser.Snowflake(channel.Id),
                        ["guild_id"] = FakeUser.Snowflake(this._guild.Id),
                        ["author"] = message.Author.ToJson(),
                        ["content"] = "seeded",
                        ["embeds"] = new JsonArray(),
                        ["attachments"] = new JsonArray(),
                        ["mentions"] = new JsonArray(),
                        ["mention_roles"] = new JsonArray(),
                        ["mention_everyone"] = false,
                        ["tts"] = false,
                        ["pinned"] = false,
                        ["type"] = 0,
                        ["timestamp"] = message.At.ToString("O"),
                        ["edited_timestamp"] = null
                    };
                    return id;
                })
            ];
        }
    }

    /// <summary>The ids still in a channel's seeded history, oldest first.</summary>
    public IReadOnlyList<ulong> HistoryIds(FakeChannel channel)
    {
        lock (this._lock)
            return [.. this.HistoryOf(channel.Id).Keys];
    }

    private SortedDictionary<ulong, JsonObject> HistoryOf(ulong channelId)
        => this._channelHistory.TryGetValue(channelId, out var history)
            ? history
            : this._channelHistory[channelId] = [];

    /// <summary>Answers <c>GET channels/{id}/messages</c> like Discord: newest first, paged by <c>before</c>.</summary>
    private JsonArray HistoryPage(ulong channelId, RecordedRequest request)
    {
        var limit = int.TryParse(request.QueryValue("limit"), out var parsed) ? parsed : 50;
        var before = ulong.TryParse(request.QueryValue("before"), out var beforeId) ? beforeId : ulong.MaxValue;
        lock (this._lock)
            return new JsonArray([
                .. this.HistoryOf(channelId).Where(message => message.Key < before).Reverse().Take(limit)
                    .Select(message => (JsonNode)message.Value.DeepClone())
            ]);
    }

    /// <summary>The bot's own post is kept in the channel's history, so it can later be fetched, edited or deleted.</summary>
    private JsonObject PostToHistory(ulong channelId, RecordedRequest request)
    {
        var message = this.MessageJson(channelId, request);
        lock (this._lock)
            this.HistoryOf(channelId)[ulong.Parse(message["id"]!.ToString())] = message;
        return (JsonObject)message.DeepClone();
    }

    private JsonObject? FromHistory(ulong channelId, ulong messageId)
    {
        lock (this._lock)
            return this.HistoryOf(channelId).TryGetValue(messageId, out var message) ? (JsonObject)message.DeepClone() : null;
    }

    private JsonObject? EditInHistory(ulong channelId, ulong messageId, RecordedRequest request)
    {
        lock (this._lock)
        {
            if (!this.HistoryOf(channelId).TryGetValue(messageId, out var message))
                return null;
            var body = request.Json as JsonObject;
            if (body?["embeds"] is { } embeds)
                message["embeds"] = embeds.DeepClone();
            if (body?["content"] is { } content)
                message["content"] = content.DeepClone();
            message["edited_timestamp"] = DateTimeOffset.UtcNow.ToString("O");
            return (JsonObject)message.DeepClone();
        }
    }

    /// <summary>The bot's posts to a channel, oldest first, as stored in its history.</summary>
    public IReadOnlyList<JsonObject> PostsIn(FakeChannel channel)
    {
        lock (this._lock)
            return [.. this.HistoryOf(channel.Id).Values
                .Where(message => message["author"]?["id"]?.ToString() == FakeUser.Snowflake(this._guild.Bot.Id))
                .Select(message => (JsonObject)message.DeepClone())];
    }

    /// <summary>A moderator deletes a message by hand.</summary>
    public void DeleteMessageByHand(FakeChannel channel, ulong messageId)
    {
        lock (this._lock)
            this.HistoryOf(channel.Id).Remove(messageId);
    }

    private HttpResponseMessage DeleteFromHistory(ulong channelId, ulong[] messageIds)
    {
        lock (this._lock)
            foreach (var id in messageIds)
                this.HistoryOf(channelId).Remove(id);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private static ulong SnowflakeAt(DateTimeOffset at, int sequence)
    {
        const long discordEpochMilliseconds = 1_420_070_400_000;
        return ((ulong)(at.ToUnixTimeMilliseconds() - discordEpochMilliseconds) << 22) | (uint)(sequence & 0xFFF);
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

    private static async Task<IReadOnlyDictionary<string, string>?> ReadFilesAsync(HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (content is not MultipartFormDataContent multipart)
            return null;
        Dictionary<string, string> files = [];
        foreach (var part in multipart)
            if (part.Headers.ContentDisposition?.FileName is { } fileName)
                files[fileName.Trim('"')] = await part.ReadAsStringAsync(cancellationToken);
        return files;
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

    [GeneratedRegex(@"^channels/(\d+)/messages/(\d+)$")]
    private static partial Regex Message();

    [GeneratedRegex(@"^guilds/\d+/bans/(\d+)$")]
    private static partial Regex GuildBan();

    [GeneratedRegex(@"^guilds/\d+/members/(\d+)$")]
    private static partial Regex GuildMember();

    [GeneratedRegex(@"^guilds/\d+/roles$")]
    private static partial Regex GuildRoles();

    [GeneratedRegex(@"^guilds/\d+/roles/(\d+)$")]
    private static partial Regex GuildRole();

    [GeneratedRegex(@"^guilds/\d+/members/(\d+)/roles/(\d+)$")]
    private static partial Regex MemberRole();

    [GeneratedRegex(@"^channels/(\d+)/permissions/(\d+)$")]
    private static partial Regex ChannelPermission();

    [GeneratedRegex(@"^guilds/\d+/audit-logs$")]
    private static partial Regex AuditLogs();

    [GeneratedRegex(@"^channels/(\d+)/messages/bulk-delete$")]
    private static partial Regex BulkDelete();

    /// <summary>Deleting one member's reaction: <c>.../reactions/{emoji}/{userId}</c>.</summary>
    [GeneratedRegex(@"^channels/\d+/messages/\d+/reactions/[^/]+/\d+$")]
    private static partial Regex Reaction();
}

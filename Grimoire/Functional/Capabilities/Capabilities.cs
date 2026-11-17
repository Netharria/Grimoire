// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Features.Shared.PluralKit.Models;
using Grimoire.Functional.Data;
using Grimoire.Functional.Discord;
using Grimoire.Functional.Telemetry;
using Grimoire.Settings.Enums;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Capabilities;

// Capabilities are the side effects the bot is allowed to have. Each is an interface whose methods return IO<A>, a
// description of the effect rather than its result, so nothing happens until a runtime runs it. A runtime
// (GrimoireRuntime, CommandRuntime) holds one implementation of each, and code asks for the ones it needs through
// Has<Eff<RT>, ...> constraints. Unit tests swap in recording fakes. Spec: docs/langext-migration/specs/1.2.

public interface ITimeIO
{
    IO<DateTimeOffset> UtcNow { get; }

    /// <summary>A monotonic timestamp, as <c>TimeProvider.GetTimestamp</c>. Durations use this, not <c>UtcNow</c>.</summary>
    IO<long> Timestamp { get; }

    IO<TimeSpan> ElapsedSince(long timestamp);
}

/// <summary>A structured log line. <c>Template</c> is a message template, so Serilog keeps the properties.</summary>
public sealed record LogEntry(
    LogLevel Level,
    string Category,
    string Template,
    Seq<object?> Args,
    Option<Exception> Exception);

public interface ILogIO
{
    IO<Unit> Write(LogEntry entry);
}

public interface IAlertIO
{
    IO<Unit> Send(Alert alert);
}

/// <summary>The guild log queue (spec 1.4, section 3.9). Features post through <c>GuildLogs&lt;RT&gt;.Post</c>.</summary>
public interface IGuildLogIO
{
    /// <summary>
    ///     Whether a log of this type would be posted: its module is on and it has a channel. A cache hit. A failed
    ///     settings lookup counts as no, since the publisher would drop the log too.
    /// </summary>
    IO<bool> IsLogged(GuildId guild, GuildLogType type);

    IO<Unit> Enqueue(GuildLogMessage message);
}

/// <summary>Setup problems shown to the guild's moderators, once a day each (GuildIssueReporter).</summary>
public interface IGuildIssueIO
{
    /// <summary>Starts the report and returns: it posts through the guild log queue, which mustn't wait on itself.</summary>
    IO<Unit> Report(GuildIssue issue);
}

/// <summary>
///     Discord through DSharpPlus (spec 1.4). Takes ids and returns snapshots, so pipelines run against a fake. Every
///     exception becomes an error case in one place (<c>DiscordErrors</c>): <c>NotFound</c> only when Discord says
///     the thing doesn't exist. <c>Discord&lt;RT&gt;</c> adds the retry for reads and idempotent calls, so it is the
///     API features use.
/// </summary>
public interface IDiscordIO
{
    IO<ChannelSnapshot> GetChannel(ChannelId channel);
    IO<UserSnapshot> GetUser(UserId user);
    IO<MemberSnapshot> GetMember(GuildId guild, UserId user);
    IO<RoleSnapshot> GetRole(GuildId guild, RoleId role);
    IO<MessageSnapshot> GetMessage(ChannelId channel, MessageId message);

    /// <summary>Creates a message. Never retried (Q25): a 502 may come after Discord already posted it.</summary>
    IO<MessageId> SendMessage(ChannelId channel, DiscordMessageBuilder message);

    /// <summary>
    ///     Opens the user's DM channel and posts. Never retried. Closed DMs are <c>Forbidden</c> with no
    ///     <c>BotRefusal</c>: that's the user's setting, not the guild's.
    /// </summary>
    IO<Unit> SendDirectMessage(UserId user, DiscordMessageBuilder message);

    /// <summary>Idempotent, so it's retried. A message that's already gone counts as deleted.</summary>
    IO<Unit> DeleteMessage(ChannelId channel, MessageId message, string reason);

    /// <summary>The schedule <c>Discord&lt;RT&gt;</c> retries on. Live: <c>DiscordRetry.Reads</c>. Fake: no delay.</summary>
    Schedule ReadRetry { get; }
}

/// <summary>
///     PluralKit's message lookup (spec 1.5). Every answer becomes a value in one place (<c>PluralKitErrors</c>):
///     <c>None</c> for a message PluralKit doesn't know (404/20006), <c>Unavailable</c> for anything that went wrong.
///     <c>PluralKit&lt;RT&gt;</c> adds the 60-second window, the retry and the cap, so it is the API features use.
/// </summary>
public interface IPluralKitIO
{
    /// <summary>False when PluralKit isn't configured. <see cref="GetMessage" /> is then never called.</summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     The message as PluralKit knows it, by the proxy's id or the original's. Waits for a token from the rate
    ///     limiter's bucket first, or fails at once when its queue is full.
    /// </summary>
    IO<Option<PluralKitMessage>> GetMessage(MessageId message);

    /// <summary>The schedule <c>PluralKit&lt;RT&gt;</c> retries on. Live: <c>PluralKitRetry.Reads</c>. Fake: no delay.</summary>
    Schedule ReadRetry { get; }

    /// <summary>The longest a whole lookup may take, queue and retries included. Live: <c>PluralKitRetry.Cap</c>.</summary>
    TimeSpan Cap { get; }
}

/// <summary>
///     Spans, span events and counters (spec 1.6). One capability, because a span and its duration metric share the
///     outcome. The live implementation is the only code that touches <c>ActivitySource</c> and <c>Meter</c>.
///     Features use it through <c>Telemetry&lt;RT&gt;</c>, and get their spans from <c>Db</c>, <c>Discord</c> and
///     <c>PluralKit</c> without asking.
/// </summary>
public interface ITelemetryIO
{
    /// <summary>
    ///     The id a user quotes from the generic error reply: the current trace id (32 hex characters), or 10 random
    ///     hex characters when no span is recording.
    /// </summary>
    IO<string> ErrorId { get; }

    /// <summary>
    ///     Runs <paramref name="body" /> inside a span. The span is current for everything the body awaits, so
    ///     Npgsql's and HttpClient's spans nest under it. When it ends, the span gets the result's outcome, tags and
    ///     status, and the operation's metric is recorded. The result passes through unchanged.
    ///     <paramref name="ended" /> describes a success; a failure is described by <c>SpanResult.OfError</c>.
    /// </summary>
    IO<A> Span<A>(Operation operation, IO<A> body, Func<A, SpanEnd> ended);

    /// <summary>An event on the current span ("retry", "guild-issue"). Does nothing when no span is recording.</summary>
    IO<Unit> Event(string name, Tags tags);

    /// <summary>Tags on the current span, for what is known only at the end (a tick's item count).</summary>
    IO<Unit> Tag(Tags tags);

    IO<Unit> Count(Counter counter, Tags tags);
}

/// <summary>How a command's replies go out. Decided once per run, before validation.</summary>
public enum Delivery
{
    Public,
    Ephemeral,
    DirectMessage
}

/// <summary>
///     The reply side of one command invocation. Only <c>CommandRuntime</c> has one, so an event handler or a worker
///     that tries to reply doesn't compile.
/// </summary>
public interface ICommandIO
{
    /// <summary>
    ///     Slash: defer, ephemeral for <see cref="Delivery.Ephemeral" />. Text, public: start the response. Text,
    ///     <see cref="Delivery.DirectMessage" />: open the invoker's DM channel, failing with <c>Forbidden</c> when
    ///     their DMs are closed.
    /// </summary>
    IO<Unit> Defer(Delivery delivery);

    /// <summary>Edits the deferred response (or posts in the DM), then sends each follow-up the same way.</summary>
    IO<Unit> Reply(Delivery delivery, DiscordEmbed embed, Seq<DiscordEmbed> followUps);

    /// <summary>
    ///     A refusal or the generic error. Slash, not yet acknowledged: an ephemeral initial response. Slash, deferred:
    ///     deletes the deferred response and follows up ephemerally (KI-21). Text: a public reply, or a DM for
    ///     <see cref="Delivery.DirectMessage" />.
    /// </summary>
    IO<Unit> Refuse(Delivery delivery, DiscordEmbed embed);

    /// <summary>Reacts to the invoking text message. Used for 🔒 when a private command is run as text.</summary>
    IO<Unit> React(string emoji);
}

/// <summary>
///     Postgres through Dapper (spec 1.3). Each call opens a pooled connection and closes it when done. Exceptions
///     become <c>Unavailable</c>, <c>Errors.Cancelled</c> or a defect in one place (<c>DatabaseErrors</c>).
///     <c>Db&lt;RT&gt;</c> adds the read retry, so it is the API features use.
/// </summary>
public interface IDatabaseIO
{
    IO<Seq<A>> Query<A>(Sql sql);

    /// <summary>The first row, or <c>None</c>; works for value types too. Give the statement a <c>LIMIT 1</c>.</summary>
    IO<Option<A>> QueryOptional<A>(Sql sql);

    /// <summary>Exactly one row. Zero or several rows is a defect: use <see cref="QueryOptional{A}" /> when absence is possible.</summary>
    IO<A> QuerySingle<A>(Sql sql);

    /// <summary>The number of rows affected.</summary>
    IO<int> Execute(Sql sql);

    /// <summary>
    ///     Runs the body in one transaction on one connection: commits when it succeeds, rolls back when it fails.
    ///     The body is <c>IO</c>, not <c>Eff</c>, so only SQL and pure code run inside: no Discord call holds a
    ///     transaction open. The session can't start another, so transactions don't nest. Never retried.
    /// </summary>
    IO<A> Transaction<A>(Func<IDatabaseSession, IO<A>> body, IsolationLevel isolation = IsolationLevel.ReadCommitted);

    /// <summary>
    ///     Streams the rows through a pure fold, holding the connection only while it runs (Q28). Never retried; a
    ///     failure part way drops the partial state. Work that calls Discord per row reads bounded pages with
    ///     <see cref="Query{A}" /> instead, so no reader stays open across it (P-15).
    /// </summary>
    IO<S> Fold<A, S>(Sql sql, S seed, Func<S, A, S> step);

    /// <summary>The schedule <c>Db&lt;RT&gt;</c> retries reads on. Live: <c>DatabaseRetry.Reads</c>.</summary>
    Schedule ReadRetry { get; }
}

/// <summary>The statements of one open transaction. No retries, no nesting.</summary>
public interface IDatabaseSession
{
    IO<Seq<A>> Query<A>(Sql sql);
    IO<Option<A>> QueryOptional<A>(Sql sql);
    IO<A> QuerySingle<A>(Sql sql);
    IO<int> Execute(Sql sql);
}

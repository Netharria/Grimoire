// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Data;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Functional.Data;
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

public interface IGuildLogIO
{
    IO<Unit> Send(GuildLogMessageBase message);
}

/// <summary>
///     The id a user quotes from the generic error reply. Until section 1.6 it is random; 1.6 makes it the trace id.
/// </summary>
public interface ITraceIO
{
    IO<string> ErrorId { get; }
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

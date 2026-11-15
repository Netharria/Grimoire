// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
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

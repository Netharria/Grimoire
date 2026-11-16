// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

// The error cases (plan C1, spec 1.1). An error is its case plus a message (Q2b). The boundary matches these four;
// any other Error is a defect.

/// <summary>Bad input, or an action the rules don't allow. Red. The message is shown to the user.</summary>
public sealed record Invalid(string Message) : Expected(Message, ErrorCase.Invalid, None);

/// <summary>The thing asked for doesn't exist. Yellow. The message is shown to the user.</summary>
public sealed record NotFound(string Message) : Expected(Message, ErrorCase.NotFound, None);

/// <summary>Discord or a permission check refused. Red. The message names what's missing (TD-12).</summary>
/// <param name="Refusal">
///     Set only when Discord refused the bot for a missing permission in a guild, so a boundary can tell the guild how
///     to fix it (spec 1.4, section 3.7). A refusal by our own rules, or a member's closed DMs, has none.
/// </param>
public sealed record Forbidden(string Message, Option<BotRefusal> Refusal = default)
    : Expected(Message, ErrorCase.Forbidden, None);

/// <summary>What Discord refused the bot: the permission it needs, and where.</summary>
/// <param name="Permission">The permission's name as Discord's settings show it, e.g. "Ban Members".</param>
/// <param name="Operation">What the bot was doing, e.g. "send a message"; part of the guild issue's code.</param>
/// <param name="AboutRoleOrder">True when the bot's role must also be above the target's (member and role actions).</param>
public sealed record BotRefusal(
    GuildId Guild,
    Option<ChannelId> Channel,
    string Permission,
    string Operation,
    bool AboutRoleOrder = false);

/// <summary>
///     A dependency is down: the database, Discord or PluralKit (Q1). The message is for the logs only. From 1.2 the
///     user sees the generic reply. <paramref name="Inner" /> keeps the exception for the log.
/// </summary>
public sealed record Unavailable(string Message, Option<Error> Inner = default)
    : Expected(Message, ErrorCase.Unavailable, Inner)
{
    /// <summary>An outage caught as an exception at the IO edge. The exception is kept for the log.</summary>
    /// <remarks>
    ///     Not <c>Error.New(exception)</c>: that turns a <see cref="TimeoutException" /> or an
    ///     <see cref="OperationCanceledException" /> into language-ext's own expected error and drops the exception.
    /// </remarks>
    public static Unavailable From(string message, Exception exception)
        => new(message, Error.New(exception.Message, exception));
}

/// <summary>
///     One fixed code per case. This isn't Q2b's per-error code: it exists only because <c>Expected</c> needs one, and
///     language-ext's <c>Is</c> and <c>@catch</c> compare codes. Grimoire code matches on the type.
/// </summary>
internal static class ErrorCase
{
    public const int Invalid = 0x4752_0001;
    public const int NotFound = 0x4752_0002;
    public const int Forbidden = 0x4752_0003;
    public const int Unavailable = 0x4752_0004;
}

public static class ErrorExtensions
{
    extension(Error error)
    {
        /// <summary>
        ///     The messages of every error inside, without duplicates, joined by "; ". A <c>ManyErrors</c> from
        ///     applicative validation gives one message per failed check.
        /// </summary>
        public string Messages => string.Join("; ", error.AsIterable().Select(e => e.Message).Distinct());
    }
}

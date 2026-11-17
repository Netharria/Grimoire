// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using DSharpPlus.Exceptions;
using LanguageExt;

namespace Grimoire.Functional.Discord;

/// <summary>What kind of exception a Discord call threw, read once so the decision below is pure data.</summary>
internal enum DiscordFailureKind
{
    /// <summary>Discord answered with an error status: <see cref="DiscordFailure.Status" /> says which.</summary>
    Response,

    /// <summary>DSharpPlus's own 429 handling gave up (a global or very long limit).</summary>
    RateLimited,

    /// <summary>A bulk delete that included messages older than 14 days.</summary>
    BulkDeleteTooOld,

    /// <summary>The request never got an answer: a timeout, a lost or refused connection.</summary>
    NoResponse,

    /// <summary>A NullReferenceException thrown inside DSharpPlus's REST client (KI-54, DSP-4).</summary>
    LibraryFault,

    /// <summary>Anything else: a bug.</summary>
    Other
}

/// <summary>A Discord failure as data. <see cref="Read" /> is the only code that looks inside DSharpPlus's exceptions.</summary>
/// <param name="Code">Discord's JSON error code (50013, 10008, …), or 0 when the body has none.</param>
/// <param name="Message">Discord's top-level JSON <c>message</c>. Never its <c>errors</c>, which can echo our content.</param>
internal sealed record DiscordFailure(
    DiscordFailureKind Kind,
    Option<HttpStatusCode> Status = default,
    int Code = 0,
    string Message = "")
{
    public static DiscordFailure Read(Exception exception) =>
        exception switch
        {
            RateLimitException => new DiscordFailure(DiscordFailureKind.RateLimited),
            DiscordException discord => FromResponse(discord),
            BulkDeleteFailedException => new DiscordFailure(DiscordFailureKind.BulkDeleteTooOld),
            TaskCanceledException or TimeoutException or HttpRequestException or IOException or SocketException
                => new DiscordFailure(DiscordFailureKind.NoResponse),
            NullReferenceException when ThrownInsideDSharpPlus(exception)
                => new DiscordFailure(DiscordFailureKind.LibraryFault),
            _ => new DiscordFailure(DiscordFailureKind.Other)
        };

    /// <summary>
    ///     The status, Discord's JSON <c>code</c> from the body, and its top-level <c>message</c>, which DSharpPlus has
    ///     already parsed into <c>JsonMessage</c>. Never the body's <c>errors</c>.
    /// </summary>
    private static DiscordFailure FromResponse(DiscordException exception) =>
        new(DiscordFailureKind.Response, exception.Response.ResponseCode, CodeOf(exception.Response.Response),
            exception.JsonMessage ?? string.Empty);

    /// <summary>Discord's error body is <c>{"code": 50013, "message": "Missing Permissions", "errors": {…}}</c>.</summary>
    private static int CodeOf(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return 0;
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement is { ValueKind: JsonValueKind.Object } root
                   && root.TryGetProperty("code", out var code) && code.TryGetInt32(out var number)
                ? number
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static bool ThrownInsideDSharpPlus(Exception exception) =>
        new StackTrace(exception).GetFrame(0)?.GetMethod()?.DeclaringType?.Namespace
            ?.StartsWith("DSharpPlus", StringComparison.Ordinal) == true;
}

/// <summary>
///     Kept inside every Discord error, so the log and the alert say which call failed and how. Its message names the
///     operation, the status and Discord's code and message, never the request's content.
/// </summary>
public sealed class DiscordCallException : Exception
{
    internal DiscordCallException(string message, bool isTransient, Exception inner, int? status = null,
        int? code = null) : base(message, inner)
    {
        this.IsTransient = isTransient;
        this.Status = status;
        this.Code = code;
    }

    /// <summary>A 5xx or no answer at all: worth retrying for a read or an idempotent call.</summary>
    public bool IsTransient { get; }

    /// <summary>The HTTP status Discord answered with; null when there was no answer. A span tag (spec 1.6).</summary>
    public int? Status { get; }

    /// <summary>Discord's JSON error code; null when it sent none. A span tag (spec 1.6).</summary>
    public int? Code { get; }
}

internal static class DiscordErrors
{
    private const int UnknownGuild = 10004;
    private const int MissingAccess = 50001;
    private const int CannotSendToUser = 50007;
    private const int MissingPermissions = 50013;

    /// <summary>The one place a Discord or HTTP exception becomes a value (spec 1.4, section 3.5).</summary>
    public static Error Classify(Exception exception, DiscordOperation operation, CancellationToken token) =>
        token.IsCancellationRequested
            ? Errors.Cancelled
            : Classify(DiscordFailure.Read(exception), operation, exception);

    /// <summary>The decision itself, over the failure as data. Pure.</summary>
    public static Error Classify(DiscordFailure failure, DiscordOperation operation, Exception exception) =>
        failure switch
        {
            { Kind: DiscordFailureKind.Response } => failure.Status.Match(
                status => FromStatus(status, failure, operation, exception),
                () => Defect(operation, "failed with no status", exception, failure)),
            { Kind: DiscordFailureKind.RateLimited } =>
                Outage(operation, "rate limited beyond DSharpPlus's own waits", false, exception, failure),
            { Kind: DiscordFailureKind.BulkDeleteTooOld } =>
                new Invalid("Discord can't bulk delete messages older than 14 days."),
            { Kind: DiscordFailureKind.NoResponse } =>
                Outage(operation, "no response (timeout or connection)", true, exception, failure),
            { Kind: DiscordFailureKind.LibraryFault } =>
                Outage(operation, "DSharpPlus failed inside its REST client (KI-54, DSP-4)", false, exception,
                    failure),
            _ => Defect(operation, "failed", exception, failure)
        };

    /// <summary>Unavailable from a 5xx or from no answer. Not a rate limit, a refusal or a defect. Pure.</summary>
    public static bool IsTransient(Error error) =>
        error is Unavailable unavailable
        && unavailable.Inner.Bind(inner => inner.Exception).Case is DiscordCallException { IsTransient: true };

    private static Error FromStatus(HttpStatusCode status, DiscordFailure failure, DiscordOperation operation,
        Exception exception) =>
        (status, failure.Code) switch
        {
            (HttpStatusCode.NotFound, _) when operation.IsGuildLookup => GuildGone(),
            (HttpStatusCode.NotFound, _) => new NotFound($"{Capitalised(operation.Target)} no longer exists."),
            // Provisional until OQ-9's check against real Discord: a 403 on the guild lookup means the bot isn't in
            // it, so it gets no BotRefusal and no guild issue goes to a guild the bot can't post in.
            (HttpStatusCode.Forbidden, MissingAccess or UnknownGuild) when operation.IsGuildLookup => GuildGone(),
            (HttpStatusCode.Forbidden, CannotSendToUser) =>
                new Forbidden("The member doesn't accept direct messages from me."),
            (HttpStatusCode.Forbidden, MissingAccess or MissingPermissions) => MissingPermission(failure, operation),
            (HttpStatusCode.Forbidden, _) => new Forbidden($"Discord refused to let me {operation.Action}."),
            (HttpStatusCode.Unauthorized, _) =>
                Defect(operation, "was rejected: Discord refused the bot's token (401)", exception, failure),
            (HttpStatusCode.BadRequest or HttpStatusCode.MethodNotAllowed, _) => new Invalid(
                failure.Message.Length > 0 ? $"Discord refused that: {failure.Message}." : "Discord refused that."),
            (HttpStatusCode.RequestEntityTooLarge, _) => new Invalid("The upload is too large for Discord."),
            ( >= HttpStatusCode.InternalServerError, _) =>
                Outage(operation, $"{(int)status} {failure.Message}".TrimEnd(), true, exception, failure),
            _ => Defect(operation, $"failed with {(int)status} (code {failure.Code})", exception, failure)
        };

    /// <summary>
    ///     "I don't have permission to ban that member. I need **Ban Members**, and my role must be above theirs."
    ///     The refusal is attached only when the call names a permission and a guild (spec 1.4, section 3.7).
    /// </summary>
    private static Forbidden MissingPermission(DiscordFailure failure, DiscordOperation operation)
    {
        // Missing Access without a named permission means the bot can't see the channel.
        var permission = operation.BotPermission.IsSome
            ? operation.BotPermission
            : failure.Code is MissingAccess ? Some("View Channel") : Option<string>.None;
        var needs = permission.Match(
            name => $" I need **{name}**" + (operation.AboutRoleOrder ? ", and my role must be above theirs." : "."),
            () => string.Empty);
        var refusal =
            from guild in operation.Guild
            from name in permission
            select new BotRefusal(guild, operation.Channel, name, operation.Action, operation.AboutRoleOrder);
        return new Forbidden($"I don't have permission to {operation.Action}.{needs}", refusal);
    }

    /// <summary>
    ///     A bug, keeping a <see cref="DiscordCallException" /> that names the call. language-ext rethrows an
    ///     exceptional error's exception while an effect runs and rebuilds the error from it, so the description has to
    ///     be the exception's own message to survive.
    /// </summary>
    private static Error Defect(DiscordOperation operation, string outcome, Exception exception,
        DiscordFailure failure) =>
        Error.New(new DiscordCallException(Describe(operation, outcome), false, exception, StatusOf(failure),
            CodeOf(failure)));

    private static NotFound GuildGone() => new("The server is no longer available to me.");

    private static Unavailable Outage(DiscordOperation operation, string what, bool transient, Exception exception,
        DiscordFailure failure) =>
        Unavailable.From("Discord could not be reached.",
            new DiscordCallException(Describe(operation, $"failed: {what}"), transient, exception, StatusOf(failure),
                CodeOf(failure)));

    private static int? StatusOf(DiscordFailure failure) => failure.Status.Map(status => (int)status).ToNullable();

    private static int? CodeOf(DiscordFailure failure) => failure.Code == 0 ? null : failure.Code;

    private static string Describe(DiscordOperation operation, string outcome) =>
        $"Discord call {operation.Name} {outcome}";

    private static string Capitalised(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

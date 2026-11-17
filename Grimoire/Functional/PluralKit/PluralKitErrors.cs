// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Grimoire.Features.Shared.PluralKit.Models;
using LanguageExt;

namespace Grimoire.Functional.PluralKit;

/// <summary>What went wrong with a PluralKit lookup. Decides the retry, the log level and the alert (spec 1.5, 3.4).</summary>
public enum PluralKitProblem
{
    /// <summary>A 5xx, a timeout or a lost connection. Retried; logged at Information.</summary>
    Outage,

    /// <summary>A 429: our bucket should have prevented it. Retried; logged at Warning.</summary>
    RateLimited,

    /// <summary>More than about 10 s of lookups already waiting. Not retried; Warning.</summary>
    QueueFull,

    /// <summary>The whole lookup ran past its cap. Information.</summary>
    TooSlow,

    /// <summary>
    ///     401, 403, 400, or a 404 without PluralKit's code 20006: the token, the user agent or the endpoint is wrong.
    ///     It won't fix itself, so Warning and a Warning alert.
    /// </summary>
    Refused,

    /// <summary>An answer that can't be read: not JSON, a changed shape, or about another message. Warning.</summary>
    Unreadable,

    /// <summary>Anything else: a bug. Error, and a Warning alert.</summary>
    Unexpected
}

/// <summary>
///     Kept inside every PluralKit error, so the log and the alert say what failed. Its message names the status and
///     PluralKit's code and error text, never message content or system and member ids.
/// </summary>
public sealed class PluralKitCallException : Exception
{
    internal PluralKitCallException(string message, PluralKitProblem problem, Exception? inner = null,
        int? status = null)
        : base(message, inner)
    {
        this.Problem = problem;
        this.Status = status;
    }

    public PluralKitProblem Problem { get; }

    /// <summary>The HTTP status PluralKit answered with; null when there was no answer. A span tag (spec 1.6).</summary>
    public int? Status { get; }

    /// <summary>Worth asking again: an outage or a rate limit.</summary>
    public bool IsTransient => this.Problem is PluralKitProblem.Outage or PluralKitProblem.RateLimited;
}

/// <summary>
///     The one place a PluralKit answer or exception becomes a value (spec 1.5, section 3.4). Pure: the live capability
///     reads the response, this decides.
/// </summary>
internal static class PluralKitErrors
{
    public const string Operation = "PluralKit.GetMessage";
    public const string UnavailableMessage = "PluralKit could not be reached.";
    private const int MessageNotFound = 20006;

    /// <summary>An answer from PluralKit: the message, <c>None</c> for 404/20006, or the error for anything else.</summary>
    public static Fin<Option<PluralKitMessage>> FromResponse(HttpStatusCode status, string body)
    {
        if (status is HttpStatusCode.OK)
            return ReadMessage(body);

        var error = ErrorBody.Read(body);
        return (status, error.Code) switch
        {
            (HttpStatusCode.NotFound, MessageNotFound) => Fin.Succ(Option<PluralKitMessage>.None),
            (HttpStatusCode.NotFound, _) => Fail(PluralKitProblem.Refused,
                "answered 404 without code 20006; check pluralkitApiEndpoint", status),
            (HttpStatusCode.TooManyRequests, _) => Fail(PluralKitProblem.RateLimited,
                $"answered 429 (retry after {error.RetryAfterMs.Match(ms => $"{ms} ms", () => "an unknown time")})", status),
            (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest, _) =>
                Fail(PluralKitProblem.Refused, $"refused the lookup: {Describe(status, error)}", status),
            ( >= HttpStatusCode.InternalServerError, _) =>
                Fail(PluralKitProblem.Outage, $"failed: {Describe(status, error)}", status),
            _ => Fail(PluralKitProblem.Unexpected, $"answered {Describe(status, error)}", status)
        };
    }

    /// <summary>An exception from the call itself. Cancellation by the run's own token is <c>Errors.Cancelled</c>.</summary>
    public static Error Classify(Exception exception, CancellationToken token) =>
        token.IsCancellationRequested
            ? Errors.Cancelled
            : exception switch
            {
                // Not our token, so HttpClient's own timeout.
                OperationCanceledException or TimeoutException or HttpRequestException or IOException
                    or SocketException => Failure(PluralKitProblem.Outage,
                        $"failed: no response (timeout or connection)", exception),
                _ => Failure(PluralKitProblem.Unexpected, $"failed: {exception.GetType().Name}", exception)
            };

    public static Unavailable QueueFull() =>
        Outage(PluralKitProblem.QueueFull, "wasn't sent: the rate limiter's queue is full", None);

    public static Unavailable TooSlow(TimeSpan cap) =>
        Outage(PluralKitProblem.TooSlow, $"took longer than {cap.TotalSeconds:0} s, queue and retries included",
            None);

    public static Unavailable Unreadable(string what) =>
        Outage(PluralKitProblem.Unreadable, $"answered something that can't be read: {what}", None);

    /// <summary>An outage or a rate limit: retrying could help. Pure.</summary>
    public static bool IsTransient(Error error) =>
        ExceptionOf(error).Case is PluralKitCallException { IsTransient: true };

    /// <summary>The problem a PluralKit error carries, or None for any other error.</summary>
    public static Option<PluralKitProblem> ProblemOf(Error error) =>
        ExceptionOf(error).Case is PluralKitCallException exception ? Some(exception.Problem) : None;

    /// <summary>The <see cref="PluralKitCallException" /> or other exception an error keeps, for the log and the alert.</summary>
    public static Option<Exception> ExceptionOf(Error error) =>
        error is Unavailable unavailable
            ? unavailable.Inner.Bind(inner => inner.Exception)
            : error.Exception;

    private static Fin<Option<PluralKitMessage>> ReadMessage(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<PluralKitMessage>(body) is { } message
                ? Fin.Succ(Some(message))
                : Fin.Fail<Option<PluralKitMessage>>(Unreadable("an empty message"));
        }
        catch (JsonException)
        {
            return Fin.Fail<Option<PluralKitMessage>>(Unreadable("not a message object"));
        }
    }

    private static Fin<Option<PluralKitMessage>> Fail(PluralKitProblem problem, string what, HttpStatusCode status) =>
        Fin.Fail<Option<PluralKitMessage>>(Failure(problem, what, null, (int)status));

    private static Error Failure(PluralKitProblem problem, string what, Exception? exception, int? status = null) =>
        problem is PluralKitProblem.Unexpected
            // A defect keeps the description as the exception's own message: language-ext rebuilds an exceptional
            // error from its exception while an effect runs (spec 1.4, section 3.13).
            ? Error.New(new PluralKitCallException(Describe(what), problem, exception, status))
            : Outage(problem, what, Optional(exception), status);

    private static Unavailable Outage(PluralKitProblem problem, string what, Option<Exception> exception,
        int? status = null) =>
        Unavailable.From(UnavailableMessage,
            new PluralKitCallException(Describe(what), problem, exception.OrNull(), status));

    private static string Describe(string what) => $"PluralKit call {Operation} {what}";

    private static string Describe(HttpStatusCode status, ErrorBody error) =>
        $"{(int)status} (code {error.Code}{(error.Message.Length > 0 ? $", {error.Message}" : string.Empty)})";

    /// <summary>PluralKit's error body: <c>{"code": 20006, "message": "Message not found.", "retry_after": 19}</c>.</summary>
    private sealed record ErrorBody(int Code, string Message, Option<int> RetryAfterMs)
    {
        public static ErrorBody Read(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return new ErrorBody(0, string.Empty, None);
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind is not JsonValueKind.Object)
                    return new ErrorBody(0, string.Empty, None);
                var root = document.RootElement;
                return new ErrorBody(
                    root.TryGetProperty("code", out var code) && code.TryGetInt32(out var number) ? number : 0,
                    root.TryGetProperty("message", out var message) && message.ValueKind is JsonValueKind.String
                        ? message.GetString() ?? string.Empty
                        : string.Empty,
                    root.TryGetProperty("retry_after", out var retry) && retry.TryGetInt32(out var ms)
                        ? Some(ms)
                        : None);
            }
            catch (JsonException)
            {
                return new ErrorBody(0, string.Empty, None);
            }
        }
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using Grimoire.Functional.PluralKit;
using LanguageExt;

namespace Grimoire.UnitTests.Functional.PluralKit;

/// <summary>Spec 1.5, section 3.4: every PluralKit answer and exception, row by row.</summary>
public sealed class PluralKitErrorsTests
{
    private const string MessageJson =
        """{"id": "200", "original": "100", "system": {"id": "abcde"}, "member": {"id": "fghij"}}""";

    private static Error Failure(HttpStatusCode status, string body = "") =>
        PluralKitErrors.FromResponse(status, body).ShouldFail();

    private static PluralKitProblem ProblemOf(Error error) =>
        PluralKitErrors.ProblemOf(error).ShouldBeSome();

    [Fact]
    public void A200_IsTheMessage()
    {
        var message = PluralKitErrors.FromResponse(HttpStatusCode.OK, MessageJson).ShouldSucceed().ShouldBeSome();

        message.Id.ShouldBe("200");
        message.OriginalId.ShouldBe("100");
        message.PluralKitSystem!.Id.ShouldBe("abcde");
        message.Member!.Id.ShouldBe("fghij");
    }

    [Fact]
    public void A200_WithExtraFields_StillReads()
        => PluralKitErrors.FromResponse(HttpStatusCode.OK,
                """{"id": "200", "original": "100", "sender": "5", "timestamp": "2026-01-01T00:00:00Z", "system": null}""")
            .ShouldSucceed().ShouldBeSome().PluralKitSystem.ShouldBeNull();

    [Theory]
    [InlineData("<html>Service unavailable</html>")]
    [InlineData("""{"id": 12345, "original": ["not", "a", "string"]}""")]
    [InlineData("null")]
    [InlineData("")]
    public void A200_ThatCantBeRead_IsUnreadable(string body)
    {
        var error = Failure(HttpStatusCode.OK, body);

        error.ShouldBeOfType<Unavailable>();
        ProblemOf(error).ShouldBe(PluralKitProblem.Unreadable);
        PluralKitErrors.IsTransient(error).ShouldBeFalse();
    }

    [Fact]
    public void A404_WithCode20006_IsNone()
        => PluralKitErrors.FromResponse(HttpStatusCode.NotFound, """{"message": "Message not found.", "code": 20006}""")
            .ShouldSucceed().IsNone.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("<html>Not Found</html>")]
    [InlineData("""{"message": "Not found", "code": 0}""")]
    public void A404_WithoutCode20006_IsARefusal_NamingTheEndpointSetting(string body)
    {
        var error = Failure(HttpStatusCode.NotFound, body);

        ProblemOf(error).ShouldBe(PluralKitProblem.Refused);
        PluralKitErrors.ExceptionOf(error).ShouldBeSome().Message.ShouldContain("pluralkitApiEndpoint");
        PluralKitErrors.IsTransient(error).ShouldBeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void A5xx_IsATransientOutage(HttpStatusCode status)
    {
        var error = Failure(status, """{"message": "Internal server error, try again later", "code": 0}""");

        error.ShouldBeOfType<Unavailable>().Message.ShouldBe(PluralKitErrors.UnavailableMessage);
        ProblemOf(error).ShouldBe(PluralKitProblem.Outage);
        PluralKitErrors.IsTransient(error).ShouldBeTrue();
    }

    [Fact]
    public void A429_IsTransient_AndKeepsRetryAfter()
    {
        var error = Failure(HttpStatusCode.TooManyRequests,
            """{"message": "429: too many requests", "retry_after": 19, "code": 0}""");

        ProblemOf(error).ShouldBe(PluralKitProblem.RateLimited);
        PluralKitErrors.IsTransient(error).ShouldBeTrue();
        PluralKitErrors.ExceptionOf(error).ShouldBeSome().Message.ShouldContain("19 ms");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Missing or invalid Authorization header")]
    [InlineData(HttpStatusCode.Forbidden, "Your access to the API is blocked")]
    [InlineData(HttpStatusCode.BadRequest, "Missing User-Agent")]
    public void ARefusal_IsNotTransient_AndQuotesPluralKitsMessage(HttpStatusCode status, string message)
    {
        var error = Failure(status, $$"""{"message": "{{message}}", "code": 0}""");

        error.ShouldBeOfType<Unavailable>();
        ProblemOf(error).ShouldBe(PluralKitProblem.Refused);
        PluralKitErrors.IsTransient(error).ShouldBeFalse();
        PluralKitErrors.ExceptionOf(error).ShouldBeSome().Message.ShouldContain(message);
    }

    [Fact]
    public void AnyOtherStatus_IsADefect()
    {
        var error = Failure(HttpStatusCode.Redirect);

        error.ShouldNotBeOfType<Unavailable>();
        error.IsExceptional.ShouldBeTrue();
        ProblemOf(error).ShouldBe(PluralKitProblem.Unexpected);
        error.Exception.ShouldBeSome().Message.ShouldContain("PluralKit.GetMessage");
    }

    [Theory]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(SocketException))]
    public void NoResponse_IsATransientOutage(Type exceptionType)
    {
        var error = PluralKitErrors.Classify((Exception)Activator.CreateInstance(exceptionType)!, CancellationToken.None);

        ProblemOf(error).ShouldBe(PluralKitProblem.Outage);
        PluralKitErrors.IsTransient(error).ShouldBeTrue();
    }

    [Fact]
    public void AnyOtherException_IsADefect()
    {
        var error = PluralKitErrors.Classify(new InvalidOperationException("boom"), CancellationToken.None);

        error.ShouldNotBeOfType<Unavailable>();
        ProblemOf(error).ShouldBe(PluralKitProblem.Unexpected);
        error.Exception.ShouldBeSome().InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Cancellation_ByTheRunsToken_WinsOverEveryException()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        PluralKitErrors.Classify(new HttpRequestException(), source.Token).Is(Errors.Cancelled).ShouldBeTrue();
        PluralKitErrors.Classify(new InvalidOperationException(), source.Token).Is(Errors.Cancelled).ShouldBeTrue();
    }

    [Fact]
    public void AFullQueue_IsNotTransient()
    {
        var error = PluralKitErrors.QueueFull();

        ProblemOf(error).ShouldBe(PluralKitProblem.QueueFull);
        PluralKitErrors.IsTransient(error).ShouldBeFalse();
    }

    [Fact]
    public void TooSlow_NamesTheCap()
    {
        var error = PluralKitErrors.TooSlow(TimeSpan.FromSeconds(15));

        ProblemOf(error).ShouldBe(PluralKitProblem.TooSlow);
        PluralKitErrors.IsTransient(error).ShouldBeFalse();
        PluralKitErrors.ExceptionOf(error).ShouldBeSome().Message.ShouldContain("15 s");
    }

    [Fact]
    public void AnErrorFromElsewhere_HasNoProblem_AndIsntTransient()
    {
        var error = new Unavailable("The database is unavailable.");

        PluralKitErrors.ProblemOf(error).IsNone.ShouldBeTrue();
        PluralKitErrors.IsTransient(error).ShouldBeFalse();
    }
}

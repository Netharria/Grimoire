// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Data;
using Grimoire.Functional.Discord;
using Grimoire.Functional.PluralKit;
using Grimoire.Functional.Telemetry;
using Grimoire.UnitTests.Functional.Data;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;

namespace Grimoire.UnitTests.Functional.Telemetry;

/// <summary>Spec 1.6, sections 3.2 and 3.3: how a finished operation becomes its outcome, status and tags.</summary>
public sealed class SpanResultTests
{
    private static readonly Sql Read = Sql.Read("Test.Count", "SELECT count(*) FROM x");

    private static SpanEnd Failed(Error error, bool shuttingDown = false) =>
        SpanResult.Of(Fin.Fail<int>(error), shuttingDown, _ => SpanResult.Succeeded());

    [Fact]
    public void ASuccess_IsLabelledByTheValue_WithNoError()
    {
        var end = SpanResult.Of(Fin.Succ(3), false, value => SpanResult.Succeeded($"Got{value}"));

        end.Outcome.ShouldBe("Got3");
        end.IsError.ShouldBeFalse();
        end.Exception.IsNone.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("NotFound")]
    [InlineData("Forbidden")]
    public void ARefusal_IsItsCase_WithoutErrorStatus(string kind)
    {
        Error error = kind switch
        {
            "Invalid" => new Invalid("bad input"),
            "NotFound" => new NotFound("gone"),
            _ => new Forbidden("no")
        };

        var end = Failed(error);

        end.Outcome.ShouldBe(kind);
        end.IsError.ShouldBeFalse();
        end.ErrorType.IsNone.ShouldBeTrue();
        end.Exception.IsNone.ShouldBeTrue();
    }

    [Fact]
    public void AnOutage_IsAnError_KeepingItsException()
    {
        var error = DatabaseErrors.Classify(new TimeoutException(), Read, false, "db:5432", CancellationToken.None);

        var end = Failed(error);

        end.Outcome.ShouldBe("Unavailable");
        end.IsError.ShouldBeTrue();
        end.ErrorType.ShouldBe(Some("Unavailable"));
        end.Exception.Case.ShouldBeOfType<DatabaseException>();
    }

    [Fact]
    public void ADefect_IsAnError_NamedByItsExceptionType()
    {
        var end = Failed(Error.New(new InvalidOperationException("a bug")));

        end.Outcome.ShouldBe("Defect");
        end.IsError.ShouldBeTrue();
        end.ErrorType.ShouldBe(Some(nameof(InvalidOperationException)));
    }

    [Fact]
    public void ACancellation_DuringShutdown_IsCancelled_WithoutError()
    {
        var end = Failed(Errors.Cancelled, shuttingDown: true);

        end.Outcome.ShouldBe("Cancelled");
        end.IsError.ShouldBeFalse();
    }

    [Fact]
    public void ACancellation_OutsideShutdown_IsAnOutage()
        => Failed(Errors.Cancelled).Outcome.ShouldBe("Unavailable");

    [Fact]
    public void ManyErrors_TakeTheirWorstCase()
    {
        var error = Error.Many(new NotFound("gone"), FakeDiscord.Outage(), new Invalid("bad"));

        var end = Failed(error);

        end.Outcome.ShouldBe("Unavailable");
        end.Tags.Find(TelemetryNames.HttpStatus).IsNone.ShouldBeTrue();
        end.Tags.Find(TelemetryNames.Transient).ShouldBe(Some((object)true));
    }

    [Theory]
    [InlineData(CommandOutcome.Success, false)]
    [InlineData(CommandOutcome.PrivateOnText, false)]
    [InlineData(CommandOutcome.Invalid, false)]
    [InlineData(CommandOutcome.NotFound, false)]
    [InlineData(CommandOutcome.Forbidden, false)]
    [InlineData(CommandOutcome.Cancelled, false)]
    [InlineData(CommandOutcome.Unavailable, true)]
    [InlineData(CommandOutcome.Defect, true)]
    public void ARootOutcome_IsAnErrorOnlyForOutagesAndDefects(CommandOutcome outcome, bool isError)
    {
        var end = SpanResult.Of(outcome);

        end.Outcome.ShouldBe(outcome.ToString());
        end.IsError.ShouldBe(isError);
    }
}

public sealed class TelemetryTagsTests
{
    private static readonly Sql Write = Sql.Write("Warn.InsertSin", "INSERT INTO x VALUES (@a)", new { a = 1 });

    [Fact]
    public void ADatabaseFailure_IsTaggedWithItsSqlStateTableAndConstraint()
    {
        var postgres = DatabaseErrorsTests.Postgres("57P01", "terminating connection: secret value 42");
        var error = DatabaseErrors.Classify(postgres, Write, false, "db:5432", CancellationToken.None);

        var tags = TelemetryTags.Of(error);

        tags.Find(TelemetryNames.Outcome).ShouldBe(Some((object)"Unavailable"));
        tags.Find(TelemetryNames.DbStatus).ShouldBe(Some((object)"57P01"));
        tags.Find(TelemetryNames.Transient).ShouldBe(Some((object)true));
        NoValueQuotes(tags, error.Message, "secret value 42");
    }

    [Fact]
    public void ADiscordOutage_IsTaggedWithItsStatusAndCode()
    {
        var failure = new DiscordFailure(DiscordFailureKind.Response, HttpStatusCode.BadGateway, 0, "Bad Gateway");
        var error = DiscordErrors.Classify(failure, new DiscordOperation("Discord.GetMember", "look up", "that member"),
            new HttpRequestException("502"));

        var tags = TelemetryTags.Of(error);

        tags.Find(TelemetryNames.HttpStatus).ShouldBe(Some((object)502));
        tags.Find(TelemetryNames.DiscordCode).IsNone.ShouldBeTrue();
        tags.Find(TelemetryNames.Transient).ShouldBe(Some((object)true));
    }

    [Fact]
    public void ADiscordDefect_KeepsDiscordsCode()
    {
        var failure = new DiscordFailure(DiscordFailureKind.Response, HttpStatusCode.Conflict, 30001, "Max reached");
        var error = DiscordErrors.Classify(failure, new DiscordOperation("Discord.SendMessage", "send", "it"),
            new HttpRequestException("409"));

        var tags = TelemetryTags.Of(error);

        tags.Find(TelemetryNames.Outcome).ShouldBe(Some((object)"Defect"));
        tags.Find(TelemetryNames.HttpStatus).ShouldBe(Some((object)409));
        tags.Find(TelemetryNames.DiscordCode).ShouldBe(Some((object)30001));
        NoValueQuotes(tags, "Max reached");
    }

    [Fact]
    public void APluralKitFailure_IsTaggedWithItsProblemAndStatus()
    {
        var error = PluralKitErrors.FromResponse(HttpStatusCode.Forbidden, """{"message":"blocked","code":0}""")
            .Match(Succ: _ => throw new InvalidOperationException("expected a failure"), Fail: e => e);

        var tags = TelemetryTags.Of(error);

        tags.Find(TelemetryNames.PluralKitProblem).ShouldBe(Some((object)"Refused"));
        tags.Find(TelemetryNames.HttpStatus).ShouldBe(Some((object)403));
        NoValueQuotes(tags, "blocked");
    }

    [Fact]
    public void ARefusedPermission_IsTaggedWithThePermission_NeverTheMessage()
    {
        var error = FakeDiscord.MissingPermission(new GuildId(10), "Ban Members");

        var tags = TelemetryTags.Of(error);

        tags.Find(TelemetryNames.Outcome).ShouldBe(Some((object)"Forbidden"));
        tags.Find(TelemetryNames.Permission).ShouldBe(Some((object)"Ban Members"));
        NoValueQuotes(tags, error.Message);
    }

    [Fact]
    public void ARefusalByOurRules_HasOnlyItsOutcome()
        => TelemetryTags.Of(new Invalid("You can't warn <@123>.")).Items.ShouldBe(
            [(TelemetryNames.Outcome, (object)"Invalid")]);

    /// <summary>No tag value holds a message, or a piece of one (defect 9).</summary>
    private static void NoValueQuotes(Tags tags, params string[] texts)
    {
        foreach (var (name, value) in tags.Items)
        foreach (var text in texts)
            value.ToString()!.ShouldNotContain(text, Case.Insensitive, $"tag {name}");
    }
}

public sealed class GrimoireSamplerTests
{
    private static readonly KeyValuePair<string, object?>[] RootTags = [new(TelemetryNames.Root, true)];

    private static ActivityContext Parent(bool recorded) =>
        new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
            recorded ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);

    [Fact]
    public void AParentlessRoot_IsKept() => GrimoireSampler.Decide(default, RootTags).ShouldBeTrue();

    [Fact]
    public void AParentlessSpanWithoutTheRootTag_IsDropped()
    {
        GrimoireSampler.Decide(default, null).ShouldBeFalse();
        GrimoireSampler.Decide(default, [new("url.full", "https://discord.com/api")]).ShouldBeFalse();
        GrimoireSampler.Decide(default, [new(TelemetryNames.Root, "true")]).ShouldBeFalse();
    }

    [Fact]
    public void AChild_FollowsItsParent()
    {
        GrimoireSampler.Decide(Parent(recorded: true), null).ShouldBeTrue();
        GrimoireSampler.Decide(Parent(recorded: false), RootTags).ShouldBeFalse();
    }
}

public sealed class UrlRedactionTests
{
    [Theory]
    [InlineData("https://discord.com/api/v10/interactions/123/aW50ZXJhY3Rpb24.dG9rZW4/callback",
        "https://discord.com/api/v10/interactions/123/{token}/callback")]
    [InlineData("https://discord.com/api/v10/webhooks/456/aW50ZXJhY3Rpb24/messages/@original",
        "https://discord.com/api/v10/webhooks/456/{token}/messages/@original")]
    [InlineData("https://discord.com/api/v10/webhooks/456/tok3n?wait=true",
        "https://discord.com/api/v10/webhooks/456/{token}?wait=true")]
    [InlineData("https://discord.com/api/v10/webhooks/456/tok3n", "https://discord.com/api/v10/webhooks/456/{token}")]
    public void ATokenInThePath_IsReplaced(string url, string redacted) => UrlRedaction.Redact(url).ShouldBe(redacted);

    [Theory]
    [InlineData("https://discord.com/api/v10/channels/1/messages/2")]
    [InlineData("https://discord.com/api/v10/guilds/1/members/2")]
    [InlineData("https://api.pluralkit.me/v2/messages/123")]
    [InlineData("https://discord.com/api/v10/webhooks/456")]
    public void AnOrdinaryRoute_IsUnchanged(string url) => UrlRedaction.Redact(url).ShouldBe(url);
}

public sealed class AlertTraceLinkTests
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string Template = "https://grafana.example/explore?trace={traceId}";

    private static Grimoire.Features.Shared.Alerts.Alert Alert(string? traceId) => new()
    {
        Severity = Grimoire.Features.Shared.Alerts.AlertSeverity.Urgent,
        Type = "Unavailable",
        Message = "probe could not reach a dependency",
        TraceId = traceId
    };

    [Fact]
    public void AnUrgentAlertWithATraceId_LinksToTheTrace()
        => Grimoire.Features.Shared.Alerts.AlertMessageFormatter.FormatUrgent(Alert(TraceId), 0, Template)
            .ShouldEndWith($"[View trace](<https://grafana.example/explore?trace={TraceId}>)");

    [Theory]
    [InlineData(null, Template)]
    [InlineData("abc123def0", Template)]
    [InlineData(TraceId, null)]
    [InlineData(TraceId, "https://grafana.example/explore")]
    public void WithoutATraceOrALink_ThereIsNoLink(string? traceId, string? template)
        => Grimoire.Features.Shared.Alerts.AlertMessageFormatter.FormatUrgent(Alert(traceId), 0, template)
            .ShouldNotContain("View trace");
}

/// <summary>
///     The processor itself, on spans from a source named as HttpClient's. The test host's fake REST handler replaces
///     the socket handler that makes those spans, so this is where the redaction of a real one is checked.
/// </summary>
public sealed class RedactingProcessorTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ActivitySource _http = new(RedactingProcessor.HttpSource);

    public RedactingProcessorTests()
    {
        this._listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, this._http),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(this._listener);
    }

    public void Dispose()
    {
        this._listener.Dispose();
        this._http.Dispose();
    }

    [Fact]
    public void AnInteractionCallbacksUrl_LosesItsToken()
    {
        using var span = this._http.StartActivity("POST", ActivityKind.Client).ShouldNotBeNull();
        span.SetTag("url.full", "https://discord.com/api/v10/interactions/123/secret.token/callback");

        new RedactingProcessor().OnEnd(span);

        span.GetTagItem("url.full").ShouldBe("https://discord.com/api/v10/interactions/123/{token}/callback");
    }

    [Fact]
    public void ASpanFromAnotherSource_IsLeftAlone()
    {
        using var other = new ActivitySource("Grimoire.Tests.NotHttp");
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, other),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        using var span = other.StartActivity("POST").ShouldNotBeNull();
        span.SetTag("url.full", "https://discord.com/api/v10/webhooks/1/tok3n");

        new RedactingProcessor().OnEnd(span);

        span.GetTagItem("url.full").ShouldBe("https://discord.com/api/v10/webhooks/1/tok3n");
    }
}

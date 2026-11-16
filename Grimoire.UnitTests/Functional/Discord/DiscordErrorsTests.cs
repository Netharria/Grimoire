// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Reflection;
using DSharpPlus.Exceptions;
using DSharpPlus.Net;
using Grimoire.Functional.Discord;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;

namespace Grimoire.UnitTests.Functional.Discord;

/// <summary>Spec 1.4, section 3.5: one test per row of the error table.</summary>
public sealed class DiscordErrorsTests
{
    private static readonly GuildId Guild = new(10);
    private static readonly ChannelId Channel = new(20);
    private static readonly Exception Cause = new InvalidOperationException("the original exception");

    private static readonly DiscordOperation Send = new("Discord.SendMessage", "send a message in <#20>", "<#20>",
        Guild, Channel, "Send Messages");

    private static readonly DiscordOperation Ban = new("Discord.Ban", "ban that member", "that member", Guild,
        BotPermission: "Ban Members", AboutRoleOrder: true);

    private static readonly DiscordOperation Lookup = new("Discord.GetMember", "look up that member", "that member",
        Guild);

    private static readonly DiscordOperation GuildLookup = new("Discord.GetGuild", "reach that server", "that server",
        Guild, IsGuildLookup: true);

    private static Error Classify(DiscordOperation operation, HttpStatusCode status, int code = 0,
        string message = "") =>
        DiscordErrors.Classify(new DiscordFailure(DiscordFailureKind.Response, status, code, message), operation,
            Cause);

    private static Error Classify(DiscordOperation operation, DiscordFailureKind kind) =>
        DiscordErrors.Classify(new DiscordFailure(kind), operation, Cause);

    [Fact]
    public void Cancellation_WinsOverEveryOtherRow()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        DiscordErrors.Classify(Make<ServerErrorException>(HttpStatusCode.BadGateway, ""), Send, cancelled.Token)
            .ShouldBe(Errors.Cancelled);
    }

    [Fact]
    public void WithoutCancellation_TheExceptionIsClassified()
        => DiscordErrors.Classify(Make<NotFoundException>(HttpStatusCode.NotFound, """{"code": 10008}"""), Lookup,
            CancellationToken.None).ShouldBeOfType<NotFound>();

    [Fact]
    public void NotFound_NamesWhatIsGone()
        => Classify(Lookup, HttpStatusCode.NotFound, 10007, "Unknown Member")
            .ShouldBeOfType<NotFound>().Message.ShouldBe("That member no longer exists.");

    [Theory]
    [InlineData(HttpStatusCode.NotFound, 10004)]
    [InlineData(HttpStatusCode.Forbidden, 50001)]
    public void TheGuildLookup_TreatsAGuildTheBotCantReach_AsGone(HttpStatusCode status, int code)
    {
        var error = Classify(GuildLookup, status, code).ShouldBeOfType<NotFound>();

        error.Message.ShouldBe("The server is no longer available to me.");
    }

    [Fact]
    public void MissingPermissions_NamesThePermission_AndCarriesTheRefusal()
    {
        var error = Classify(Send, HttpStatusCode.Forbidden, 50013, "Missing Permissions").ShouldBeOfType<Forbidden>();

        error.Message.ShouldBe("I don't have permission to send a message in <#20>. I need **Send Messages**.");
        error.Refusal.ShouldBe(new BotRefusal(Guild, Channel, "Send Messages", "send a message in <#20>"));
    }

    [Fact]
    public void MissingPermissions_OnAMemberAction_AlsoNamesTheRoleOrder()
    {
        var error = Classify(Ban, HttpStatusCode.Forbidden, 50013).ShouldBeOfType<Forbidden>();

        error.Message.ShouldBe(
            "I don't have permission to ban that member. I need **Ban Members**, and my role must be above theirs.");
        error.Refusal.Case.ShouldBeOfType<BotRefusal>().AboutRoleOrder.ShouldBeTrue();
    }

    [Fact]
    public void MissingAccess_WithoutANamedPermission_IsViewChannel()
    {
        var operation = Lookup with { Channel = Channel };

        var error = Classify(operation, HttpStatusCode.Forbidden, 50001).ShouldBeOfType<Forbidden>();

        error.Message.ShouldBe("I don't have permission to look up that member. I need **View Channel**.");
        error.Refusal.Case.ShouldBeOfType<BotRefusal>().Permission.ShouldBe("View Channel");
    }

    [Fact]
    public void MissingPermissions_WithoutANamedPermission_NamesNone_AndCarriesNoRefusal()
    {
        var error = Classify(Lookup, HttpStatusCode.Forbidden, 50013).ShouldBeOfType<Forbidden>();

        error.Message.ShouldBe("I don't have permission to look up that member.");
        error.Refusal.IsNone.ShouldBeTrue();
    }

    [Fact]
    public void ARefusalOutsideAGuild_CarriesNoRefusal()
        => Classify(Send with { Guild = None }, HttpStatusCode.Forbidden, 50013)
            .ShouldBeOfType<Forbidden>().Refusal.IsNone.ShouldBeTrue();

    [Fact]
    public void ClosedDirectMessages_AreForbidden_WithNoRefusal()
    {
        var error = Classify(Send, HttpStatusCode.Forbidden, 50007, "Cannot send messages to this user")
            .ShouldBeOfType<Forbidden>();

        error.Message.ShouldBe("The member doesn't accept direct messages from me.");
        error.Refusal.IsNone.ShouldBeTrue();
    }

    [Fact]
    public void AnotherForbidden_NamesTheAction_WithNoRefusal()
    {
        var error = Classify(Send, HttpStatusCode.Forbidden, 40001).ShouldBeOfType<Forbidden>();

        error.Message.ShouldBe("Discord refused to let me send a message in <#20>.");
        error.Refusal.IsNone.ShouldBeTrue();
    }

    [Fact]
    public void Unauthorized_IsADefect_NotARefusal()
    {
        var error = Classify(Send, HttpStatusCode.Unauthorized);

        error.ShouldNotBeAssignableTo<Expected>();
        error.Message.ShouldContain("401");
        error.Exception.Case.ShouldBeOfType<DiscordCallException>().InnerException.ShouldBe(Cause);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public void BadRequest_IsInvalid_QuotingDiscordsMessage(HttpStatusCode status)
        => Classify(Send, status, 50035, "Invalid Form Body")
            .ShouldBeOfType<Invalid>().Message.ShouldBe("Discord refused that: Invalid Form Body.");

    [Fact]
    public void BadRequest_WithNoMessage_SaysOnlyThatDiscordRefused()
        => Classify(Send, HttpStatusCode.BadRequest).ShouldBeOfType<Invalid>().Message
            .ShouldBe("Discord refused that.");

    [Fact]
    public void TooLarge_IsInvalid()
        => Classify(Send, HttpStatusCode.RequestEntityTooLarge).ShouldBeOfType<Invalid>().Message
            .ShouldBe("The upload is too large for Discord.");

    [Fact]
    public void ABulkDeleteOfOldMessages_IsInvalid()
        => Classify(Send, DiscordFailureKind.BulkDeleteTooOld).ShouldBeOfType<Invalid>().Message
            .ShouldBe("Discord can't bulk delete messages older than 14 days.");

    [Theory]
    [InlineData(nameof(DiscordFailureKind.NoResponse), "no response (timeout or connection)", true)]
    [InlineData(nameof(DiscordFailureKind.RateLimited), "rate limited beyond DSharpPlus's own waits", false)]
    public void AnOutage_SaysWhatHappened_ForTheLog(string kind, string what, bool transient)
    {
        var kept = Classify(Send, Enum.Parse<DiscordFailureKind>(kind)).ShouldBeOfType<Unavailable>().Inner.Bind(inner => inner.Exception).Case
            .ShouldBeOfType<DiscordCallException>();

        kept.Message.ShouldBe($"Discord call Discord.SendMessage failed: {what}");
        kept.IsTransient.ShouldBe(transient);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void AServerError_IsATransientOutage(HttpStatusCode status)
    {
        var error = Classify(Send, status).ShouldBeOfType<Unavailable>();

        error.Message.ShouldBe("Discord could not be reached.");
        DiscordErrors.IsTransient(error).ShouldBeTrue();
        var kept = error.Inner.Bind(inner => inner.Exception).Case.ShouldBeOfType<DiscordCallException>();
        kept.Message.ShouldStartWith($"Discord call Discord.SendMessage failed: {(int)status}");
        kept.InnerException.ShouldBe(Cause);
    }

    [Fact]
    public void NoResponse_IsATransientOutage()
        => DiscordErrors.IsTransient(Classify(Send, DiscordFailureKind.NoResponse).ShouldBeOfType<Unavailable>())
            .ShouldBeTrue();

    [Fact]
    public void ARateLimit_IsAnOutage_ButNotRetried()
        => DiscordErrors.IsTransient(Classify(Send, DiscordFailureKind.RateLimited).ShouldBeOfType<Unavailable>())
            .ShouldBeFalse();

    [Fact]
    public void TheDSharpPlusFault_IsContainedAsAnOutage_ButNotRetried()
    {
        var error = Classify(Send, DiscordFailureKind.LibraryFault).ShouldBeOfType<Unavailable>();

        DiscordErrors.IsTransient(error).ShouldBeFalse();
        error.Inner.Bind(inner => inner.Exception).Case.ShouldBeOfType<DiscordCallException>()
            .Message.ShouldContain("KI-54");
    }

    [Fact]
    public void AnythingElse_IsADefect()
    {
        var error = Classify(Send, DiscordFailureKind.Other);

        error.ShouldNotBeAssignableTo<Expected>();
        var kept = error.Exception.Case.ShouldBeOfType<DiscordCallException>();
        kept.Message.ShouldBe("Discord call Discord.SendMessage failed");
        kept.InnerException.ShouldBe(Cause);
    }

    [Fact]
    public async Task ADefectsDescription_SurvivesRunningTheEffect()
    {
        var defect = Classify(Send, HttpStatusCode.Unauthorized);

        var run = await new TestEnv().Run(Grimoire.Functional.Effects.Effect.Done<Grimoire.Functional.Runtime.GrimoireRuntime>()
            .Bind(_ => Eff<Grimoire.Functional.Runtime.GrimoireRuntime, LanguageExt.Unit>.LiftIO(IO.fail<LanguageExt.Unit>(defect))));

        run.ShouldFail().Message.ShouldContain("401");
    }

    [Fact]
    public void AnUnknownStatus_IsADefect_NamingTheStatusAndCode()
    {
        var error = Classify(Send, HttpStatusCode.Conflict, 40333);

        error.ShouldNotBeAssignableTo<Expected>();
        var kept = error.Exception.Case.ShouldBeOfType<DiscordCallException>();
        kept.Message.ShouldBe("Discord call Discord.SendMessage failed with 409 (code 40333)");
        kept.IsTransient.ShouldBeFalse();
    }

    [Fact]
    public void TheRetrySchedule_IsThreeRetries_UnderFourSeconds()
    {
        var delays = DiscordRetry.Reads.Run().Take(10).Select(delay => (double)delay).ToList();

        delays.Count.ShouldBe(3);
        delays[0].ShouldBeInRange(450, 550);
        delays[1].ShouldBeInRange(900, 1100);
        delays[2].ShouldBeInRange(1800, 2200);
        delays.Sum().ShouldBeLessThan(4000);
        delays.ShouldNotBe([500d, 1000d, 2000d]);
    }

    // DiscordFailure.Read, against DSharpPlus's real exceptions. Their constructors are internal, so they're built by
    // reflection: if a DSharpPlus upgrade changes them, these fail first.

    [Fact]
    public void Read_TakesTheStatusAndCode_FromTheBody()
    {
        var failure = DiscordFailure.Read(Make<UnauthorizedException>(HttpStatusCode.Forbidden,
            """{"code": 50013, "message": "Missing Permissions"}"""));

        failure.ShouldBe(new DiscordFailure(DiscordFailureKind.Response, HttpStatusCode.Forbidden, 50013,
            "Missing Permissions"));
    }

    [Fact]
    public void Read_KeepsDiscordsTopLevelMessage_NeverItsErrors()
    {
        var failure = DiscordFailure.Read(Make<BadRequestException>(HttpStatusCode.BadRequest,
            """{"code": 50035, "message": "Invalid Form Body", "errors": {"content": "SECRET-CONTENT"}}"""));

        failure.Code.ShouldBe(50035);
        failure.Message.ShouldBe("Invalid Form Body");
        Classify(Send, HttpStatusCode.BadRequest, failure.Code, failure.Message).Message
            .ShouldNotContain("SECRET-CONTENT");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    public void Read_WithABodyThatIsntAnObject_HasNoCode(string body)
    {
        DiscordFailure.Read(Make<ServerErrorException>(HttpStatusCode.BadGateway, body)).Code.ShouldBe(0);
    }

    [Fact]
    public void Read_ABodyWithoutAMessage_HasAnEmptyMessage()
        => DiscordFailure.Read(Make<UnauthorizedException>(HttpStatusCode.Forbidden, """{"code": 50013}"""))
            .ShouldBe(new DiscordFailure(DiscordFailureKind.Response, HttpStatusCode.Forbidden, 50013, ""));

    [Fact]
    public void Read_ANotFound_KeepsItsStatus()
        => DiscordFailure.Read(Make<NotFoundException>(HttpStatusCode.NotFound, """{"code": 10008}"""))
            .Status.ShouldBe(HttpStatusCode.NotFound);

    [Fact]
    public void Read_ARateLimit_IsRateLimited()
        => DiscordFailure.Read(Make<RateLimitException>(HttpStatusCode.TooManyRequests, """{"retry_after": 1}"""))
            .Kind.ShouldBe(DiscordFailureKind.RateLimited);

    [Theory]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(IOException))]
    public void Read_NoAnswer_IsNoResponse(Type exception)
        => DiscordFailure.Read((Exception)Activator.CreateInstance(exception)!).Kind
            .ShouldBe(DiscordFailureKind.NoResponse);

    [Fact]
    public void Read_ANullReferenceInOurOwnCode_IsADefect_NotTheLibraryFault()
    {
        Exception thrown;
        try
        {
            string? missing = null;
            _ = missing!.Length;
            throw new InvalidOperationException("unreachable");
        }
        catch (NullReferenceException exception)
        {
            thrown = exception;
        }

        DiscordFailure.Read(thrown).Kind.ShouldBe(DiscordFailureKind.Other);
    }

    [Fact]
    public void TheFakesOutage_IsTransientLikeTheLiveOne()
        => DiscordErrors.IsTransient(FakeDiscord.Outage()).ShouldBeTrue();

    private static T Make<T>(HttpStatusCode status, string body) where T : DiscordException
    {
        object response = default(RestResponse);
        typeof(RestResponse).GetProperty(nameof(RestResponse.ResponseCode))!.SetValue(response, status);
        typeof(RestResponse).GetProperty(nameof(RestResponse.Response))!.SetValue(response, body);
        return (T)Activator.CreateInstance(typeof(T), BindingFlags.NonPublic | BindingFlags.Instance, null,
            [new HttpRequestMessage(HttpMethod.Post, "https://discord.test/api/v10/channels/20/messages"),
                (RestResponse)response], null)!;
    }
}

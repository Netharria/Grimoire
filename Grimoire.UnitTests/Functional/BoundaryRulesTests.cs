// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Reflection;
using DSharpPlus.Commands;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Capabilities;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Structs;
using LanguageExt;

namespace Grimoire.UnitTests.Functional;

public sealed class DeliveryRulesTests
{
    public static TheoryData<Privacy, InvocationKind, Delivery?> Cases => new()
    {
        { Privacy.Public, InvocationKind.Slash, Delivery.Public },
        { Privacy.Public, InvocationKind.Text, Delivery.Public },
        { Privacy.Private, InvocationKind.Slash, Delivery.Ephemeral },
        { Privacy.Private, InvocationKind.Text, null },
        { Privacy.PrivateByDirectMessage, InvocationKind.Slash, Delivery.Ephemeral },
        { Privacy.PrivateByDirectMessage, InvocationKind.Text, Delivery.DirectMessage }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void EachPrivacyAndKind_HasItsDelivery(Privacy privacy, InvocationKind kind, Delivery? expected)
        => DeliveryRules.For(privacy, kind).ToNullable().ShouldBe(expected);

    [Fact]
    public void TheAttribute_IsPrivate_OrByDirectMessageWhenAsked()
    {
        new PrivateReplyAttribute().Privacy.ShouldBe(Privacy.Private);
        new PrivateReplyAttribute { DirectMessageOnText = true }.Privacy.ShouldBe(Privacy.PrivateByDirectMessage);
    }

    [Fact]
    public void TheAttribute_IsOnlyOnCommandMethods()
        => typeof(CommandRunner).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                                                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<PrivateReplyAttribute>() is not null)
            .Where(method => method.GetCustomAttribute<CommandAttribute>() is null)
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
            .ShouldBeEmpty();
}

public sealed class OutcomeTests
{
    [Fact]
    public void EachGrimoireCase_IsItsOutcome()
    {
        Outcome.Classify(new Invalid("x"), false).ShouldBe(CommandOutcome.Invalid);
        Outcome.Classify(new NotFound("x"), false).ShouldBe(CommandOutcome.NotFound);
        Outcome.Classify(new Forbidden("x"), false).ShouldBe(CommandOutcome.Forbidden);
        Outcome.Classify(new Unavailable("x"), false).ShouldBe(CommandOutcome.Unavailable);
    }

    [Fact]
    public void AnythingElse_IsADefect()
    {
        Outcome.Classify(Error.New(new InvalidOperationException("boom")), false).ShouldBe(CommandOutcome.Defect);
        Outcome.Classify(Error.New("an expected error that isn't a Grimoire case"), false)
            .ShouldBe(CommandOutcome.Defect);
    }

    [Fact]
    public void AnOptionBoundWithoutACase_IsADefect()
        => Outcome.Classify(Errors.None, false).ShouldBe(CommandOutcome.Defect);

    [Fact]
    public void ACancellation_IsCancelledOnlyWhileShuttingDown()
    {
        Outcome.Classify(Errors.Cancelled, true).ShouldBe(CommandOutcome.Cancelled);
        Outcome.Classify(Errors.Cancelled, false).ShouldBe(CommandOutcome.Unavailable);
        Outcome.Classify(Error.New(new TaskCanceledException()), false).ShouldBe(CommandOutcome.Unavailable);
    }

    [Fact]
    public void ATimeout_IsUnavailable()
    {
        Outcome.Classify(Errors.TimedOut, false).ShouldBe(CommandOutcome.Unavailable);
        Outcome.Classify(Error.New("slow", (Exception)new TimeoutException()), false).ShouldBe(CommandOutcome.Unavailable);
    }

    public static TheoryData<Error, CommandOutcome> Mixes => new()
    {
        { new NotFound("a") + new Invalid("b"), CommandOutcome.Invalid },
        { new Invalid("a") + new Forbidden("b"), CommandOutcome.Forbidden },
        { new Forbidden("a") + new Unavailable("b"), CommandOutcome.Unavailable },
        { new Unavailable("a") + Error.New(new InvalidOperationException()), CommandOutcome.Defect },
        { new NotFound("a") + new NotFound("b"), CommandOutcome.NotFound }
    };

    [Theory]
    [MemberData(nameof(Mixes))]
    public void SeveralErrors_TakeTheWorstCase(Error error, CommandOutcome expected)
        => Outcome.Classify(error, false).ShouldBe(expected);

    [Fact]
    public void ShutdownOutranksEverything()
        => Outcome.Classify(Error.New(new InvalidOperationException()) + Errors.Cancelled, true)
            .ShouldBe(CommandOutcome.Cancelled);

    [Fact]
    public void OnlyTheThreeUserFacingCases_AreRefusals()
        => Enum.GetValues<CommandOutcome>().Where(Outcome.IsRefusal)
            .ShouldBe([CommandOutcome.Invalid, CommandOutcome.NotFound, CommandOutcome.Forbidden],
                ignoreOrder: true);

    [Fact]
    public void ExceptionOf_FindsTheExceptionAnUnavailableKeeps()
    {
        var exception = new TimeoutException("db");
        Boundary<GrimoireTestRuntime>.ExceptionOf(Unavailable.From("down", exception))
            .Map(found => found.Message).ShouldBe(Some("db"));
        Boundary<GrimoireTestRuntime>.ExceptionOf(new Invalid("x")).IsNone.ShouldBeTrue();
    }
}

public sealed class RepliesTests
{
    [Fact]
    public void InvalidAndForbidden_AreRed_NotFoundIsYellow()
    {
        Replies.Refusal(new Invalid("x")).Color.ShouldBe(GrimoireColor.Red);
        Replies.Refusal(new Forbidden("x")).Color.ShouldBe(GrimoireColor.Red);
        Replies.Refusal(new NotFound("x")).Color.ShouldBe(GrimoireColor.Yellow);
    }

    [Fact]
    public void AMix_IsRedIfAnyIsRed()
    {
        Replies.Refusal(new NotFound("a") + new Invalid("b")).Color.ShouldBe(GrimoireColor.Red);
        Replies.Refusal(new NotFound("a") + new NotFound("b")).Color.ShouldBe(GrimoireColor.Yellow);
    }

    [Fact]
    public void EachDistinctMessage_IsOnItsOwnLine_WithNoTitle()
    {
        var embed = Replies.Refusal(new Invalid("You cannot warn yourself.") + new Invalid("The reason can't be empty.")
                                                                        + new Invalid("You cannot warn yourself."));

        embed.Description.ShouldBe("You cannot warn yourself.\nThe reason can't be empty.");
        embed.Author.ShouldBeNull();
    }

    [Fact]
    public void TheGenericReply_NamesTheCommandAndTheId_AndNothingElse()
    {
        var embed = Replies.Generic("Warn", "a1b2c3d4e5");

        embed.Description.ShouldBe("Something went wrong while running `/Warn`. It's been reported. "
                                   + "If it keeps happening, quote ID `a1b2c3d4e5`.");
        embed.Color.ShouldBe(GrimoireColor.Yellow);
    }

    [Fact]
    public void Pages_AreTheReplyAndItsFollowUps()
    {
        var reply = Reply.Pages(GrimoireColor.Purple, "Log", ["one", "two", "three"]);

        reply.Embed.Description.ShouldBe("one");
        reply.FollowUps.Map(page => page.Description).ShouldBe(["two", "three"]);
        reply.FollowUps.ForAll(page => page.Author?.Name == "Log").ShouldBeTrue();
    }

    [Fact]
    public void Logged_PostsTheSameEmbed()
    {
        var reply = Reply.Of(GrimoireColor.Purple, "done").Logged(new GuildId(1), GuildLogType.Moderation);

        reply.Log.Case.ShouldBeOfType<GuildLogMessageCustomEmbed>()
            .Embed.ShouldBe(reply.Embed);
    }
}

/// <summary>A runtime with only a log, for the helpers that need nothing else.</summary>
public sealed record GrimoireTestRuntime : LanguageExt.Traits.Has<Eff<GrimoireTestRuntime>, ILogIO>
{
    static LanguageExt.Traits.K<Eff<GrimoireTestRuntime>, ILogIO>
        LanguageExt.Traits.Has<Eff<GrimoireTestRuntime>, ILogIO>.Ask =>
        Eff<GrimoireTestRuntime, ILogIO>.Fail(Error.New("no log in this runtime"));
}

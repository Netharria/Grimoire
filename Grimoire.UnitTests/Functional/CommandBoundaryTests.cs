// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using Grimoire.Settings.Enums;
using Grimoire.Structs;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;
using Microsoft.Extensions.Logging;
using static Grimoire.UnitTests.Functional.Fakes.CommandCall;

namespace Grimoire.UnitTests.Functional;

/// <summary>Spec 1.2, section 4: one test per row of the command table, run against the fake runtime.</summary>
public sealed class CommandBoundaryTests
{
    private static readonly GuildId Guild = new(10);
    private static readonly DateTimeOffset RanAt = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly Reply Done =
        Reply.Of(GrimoireColor.Purple, "done").Logged(Guild, GuildLogType.Moderation, RanAt);

    private readonly TestEnv _env = new();
    private bool _acted;

    private static Invocation Slash { get; } = new("Probe", InvocationKind.Slash, Guild, new ChannelId(20),
        new UserId(30), DiscordPermissions.None, DateTimeOffset.UnixEpoch);

    private static Invocation Text { get; } = Slash with { Kind = InvocationKind.Text };

    private Task<Fin<CommandOutcome>> Run(Invocation invocation, Privacy privacy,
        Validation<Error, int> input, Func<int, Eff<CommandRuntime, Reply>>? act = null) =>
        this.Run(invocation, Eff<CommandRuntime, Privacy>.Pure(privacy), _ => input, act);

    private Task<Fin<CommandOutcome>> Run(Invocation invocation, Eff<CommandRuntime, Privacy> privacy,
        Func<Invocation, Validation<Error, int>> input, Func<int, Eff<CommandRuntime, Reply>>? act = null) =>
        this._env.Run(CommandBoundary<CommandRuntime>.Run(invocation, privacy, input, act ?? this.Succeeds));

    private Eff<CommandRuntime, Reply> Succeeds(int _) =>
        Eff<CommandRuntime, Reply>.Lift(() =>
        {
            this._acted = true;
            return Done;
        });

    private Func<int, Eff<CommandRuntime, Reply>> Fails(Error error) =>
        _ => Eff<CommandRuntime, Reply>.Lift(() =>
        {
            this._acted = true;
            return Fin.Fail<Reply>(error);
        });

    private static Validation<Error, int> Valid => Success<Error, int>(1);

    [Fact]
    public async Task Success_DefersThenRepliesThenPostsTheLog()
    {
        (await this.Run(Slash, Privacy.Public, Valid)).ShouldSucceed().ShouldBe(CommandOutcome.Success);

        this._env.Command.Calls.ShouldBe([new Deferred(Delivery.Public), new Replied(Delivery.Public, Done.Embed, [])]);
        this._env.Journal.Entries.ShouldBe(["deferred", "replied", "guild log gate Moderation", "guild log"]);
        this._env.Alerts.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task APrivateSlashCommand_DefersEphemerally()
    {
        await this.Run(Slash, Privacy.Private, Valid);

        this._env.Command.Calls[0].ShouldBe(new Deferred(Delivery.Ephemeral));
    }

    [Fact]
    public async Task FollowUps_GoWithTheReply()
    {
        var pages = Reply.Pages(GrimoireColor.Purple, "Log", ["one", "two"]);

        await this.Run(Slash, Privacy.Public, Valid, _ => Eff<CommandRuntime, Reply>.Pure(pages));

        this._env.Command.Calls[1].ShouldBeOfType<Replied>().FollowUps.ShouldBe(pages.FollowUps);
    }

    [Fact]
    public async Task AFailedCheck_IsRefusedAtOnce_WithoutADefer()
    {
        var outcome = await this.Run(Slash, Privacy.Public, new Invalid("You cannot warn yourself."));

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Invalid);
        var refused = this._env.Command.Calls.ShouldHaveSingleItem().ShouldBeOfType<Refused>();
        refused.Delivery.ShouldBe(Delivery.Ephemeral);
        refused.Embed.Description.ShouldBe("You cannot warn yourself.");
        refused.Embed.Color.ShouldBe(GrimoireColor.Red);
        this._acted.ShouldBeFalse();
        this._env.Alerts.Sent.ShouldBeEmpty();
        this._env.Log.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Information && entry.Template.StartsWith("Command {Command} rejected"));
    }

    [Fact]
    public async Task SeveralFailedChecks_AreOneRefusal_OneMessagePerLine()
    {
        await this.Run(Slash, Privacy.Public, new Invalid("a") + new Invalid("b"));

        this._env.Command.Calls.ShouldHaveSingleItem().ShouldBeOfType<Refused>().Embed.Description.ShouldBe("a\nb");
    }

    [Fact]
    public async Task APublicTextCommand_IsRefusedPublicly()
    {
        await this.Run(Text, Privacy.Public, new Invalid("x"));

        this._env.Command.Calls.ShouldHaveSingleItem().ShouldBeOfType<Refused>().Delivery.ShouldBe(Delivery.Public);
    }

    [Fact]
    public async Task APrivateCommandRunAsText_ReactsWithALock_AndRunsNothing()
    {
        var inputRead = false;

        var outcome = await this.Run(Text, Eff<CommandRuntime, Privacy>.Pure(Privacy.Private), _ =>
        {
            inputRead = true;
            return Valid;
        });

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.PrivateOnText);
        this._env.Command.Calls.ShouldBe([new Reacted("🔒")]);
        inputRead.ShouldBeFalse();
        this._acted.ShouldBeFalse();
    }

    [Fact]
    public async Task AFailedLockReaction_IsOnlyLogged()
    {
        this._env.Command.ReactFails = new NotFound("message deleted");

        (await this.Run(Text, Privacy.Private, Valid)).ShouldSucceed().ShouldBe(CommandOutcome.PrivateOnText);

        this._env.Alerts.Sent.ShouldBeEmpty();
        this._env.Log.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task ADirectMessageCommandRunAsText_RepliesByDirectMessage()
    {
        await this.Run(Text, Privacy.PrivateByDirectMessage, Valid);

        this._env.Command.Calls.ShouldBe([
            new Deferred(Delivery.DirectMessage), new Replied(Delivery.DirectMessage, Done.Embed, [])
        ]);
    }

    [Fact]
    public async Task ADirectMessageCommandRunAsText_IsRefusedByDirectMessage()
    {
        await this.Run(Text, Privacy.PrivateByDirectMessage, new Invalid("x"));

        this._env.Command.Calls.ShouldHaveSingleItem().ShouldBeOfType<Refused>().Delivery
            .ShouldBe(Delivery.DirectMessage);
    }

    [Fact]
    public async Task ClosedDirectMessages_AtTheReply_GetTheLockInstead()
    {
        this._env.Command.ReplyFails = new Forbidden("DMs closed");

        var outcome = await this.Run(Text, Privacy.PrivateByDirectMessage, Valid);

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Forbidden);
        this._env.Command.Calls[^1].ShouldBe(new Reacted("🔒"));
        this._env.Alerts.Sent.ShouldBeEmpty();
        this._env.GuildLog.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task ClosedDirectMessages_AtTheDefer_GetTheLock_AndNothingRuns()
    {
        this._env.Command.DeferFails = new Forbidden("DMs closed");

        await this.Run(Text, Privacy.PrivateByDirectMessage, Valid);

        this._env.Command.Calls.ShouldBe([new Deferred(Delivery.DirectMessage), new Reacted("🔒")]);
        this._acted.ShouldBeFalse();
    }

    [Fact]
    public async Task ClosedDirectMessages_ForARefusal_GetTheLockInstead()
    {
        this._env.Command.RefuseFails = new Forbidden("DMs closed");

        await this.Run(Text, Privacy.PrivateByDirectMessage, new Invalid("x"));

        this._env.Command.Calls[^1].ShouldBe(new Reacted("🔒"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ARefusalFromTheAct_ComesAfterTheDefer(bool notFound)
    {
        Error error = notFound ? new NotFound("No such sin.") : new Forbidden("Missing Ban Members.");

        var outcome = await this.Run(Slash, Privacy.Public, Valid, this.Fails(error));

        outcome.ShouldSucceed().ShouldBe(notFound ? CommandOutcome.NotFound : CommandOutcome.Forbidden);
        this._env.Command.Calls[0].ShouldBe(new Deferred(Delivery.Public));
        var refused = this._env.Command.Calls[1].ShouldBeOfType<Refused>();
        refused.Delivery.ShouldBe(Delivery.Ephemeral);
        refused.Embed.Description.ShouldBe(error.Message);
        refused.Embed.Color.ShouldBe(notFound ? GrimoireColor.Yellow : GrimoireColor.Red);
        this._env.GuildLog.Sent.ShouldBeEmpty();
        this._env.Alerts.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnOutage_IsLoggedAndAlerted_BeforeTheGenericReply()
    {
        var outcome = await this.Run(Slash, Privacy.Public, Valid,
            this.Fails(Unavailable.From("Settings database down.", new TimeoutException("db"))));

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Unavailable);
        this._env.Journal.Entries.ShouldBe(["deferred", "alert Unavailable", "refused"]);
        var alert = this._env.Alerts.Sent.ShouldHaveSingleItem();
        alert.Severity.ShouldBe(AlertSeverity.Urgent);
        alert.Discriminator.ShouldBe("Probe");
        alert.GuildId.ShouldBe(Guild);
        alert.Message.ShouldBe($"Probe could not reach a dependency [Id `{FakeTelemetry.Id}`]: Settings database down.");
        alert.Exception.ShouldBeOfType<TimeoutException>();
        this._env.Command.Calls[1].ShouldBeOfType<Refused>().Embed.Description
            .ShouldBe(Replies.Generic("Probe", FakeTelemetry.Id).Description);
        this._env.Log.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Error && entry.Args.Exists(arg => Equals(arg, FakeTelemetry.Id)));
    }

    [Fact]
    public async Task AnOutage_NeverShowsItsMessage_EvenMixedWithARefusal()
    {
        await this.Run(Slash, Privacy.Public, Valid, this.Fails(new Invalid("shown?") + new Unavailable("secret")));

        var reply = this._env.Command.Calls[1].ShouldBeOfType<Refused>().Embed.Description ?? string.Empty;
        reply.ShouldNotContain("shown?");
        reply.ShouldNotContain("secret");
    }

    [Fact]
    public async Task AnExceptionInTheAct_IsADefect()
    {
        var outcome = await this.Run(Slash, Privacy.Public, Valid,
            _ => Eff<CommandRuntime, Reply>.Lift(() => throw new InvalidOperationException("bug")));

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Defect);
        var alert = this._env.Alerts.Sent.ShouldHaveSingleItem();
        alert.Type.ShouldBe("UnhandledException");
        alert.Severity.ShouldBe(AlertSeverity.Urgent);
        alert.Message.ShouldBe($"Encountered exception while executing Probe [Id `{FakeTelemetry.Id}`]");
        alert.Exception.ShouldBeOfType<InvalidOperationException>();
        this._env.Command.Calls[^1].ShouldBeOfType<Refused>();
    }

    [Fact]
    public async Task AThrowWhileBuildingTheAct_IsADefect()
    {
        var outcome = await this.Run(Slash, Privacy.Public, Valid,
            _ => throw new InvariantViolationException("needs [RequireGuild]"));

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Defect);
        this._env.Alerts.Sent.ShouldHaveSingleItem().Type.ShouldBe("UnhandledException");
    }

    [Fact]
    public async Task AThrowWhileBuildingTheInput_IsADefect_RepliedBeforeAnyDefer()
    {
        var outcome = await this.Run(Slash, Eff<CommandRuntime, Privacy>.Pure(Privacy.Public),
            _ => throw new InvariantViolationException("no guild"));

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Defect);
        this._env.Command.Calls.ShouldHaveSingleItem().ShouldBeOfType<Refused>().Delivery
            .ShouldBe(Delivery.Ephemeral);
        this._acted.ShouldBeFalse();
    }

    [Fact]
    public async Task AnOptionBoundWithoutACase_IsADefect()
    {
        var outcome = await this.Run(Slash, Privacy.Public, Valid,
            _ => from missing in Eff<CommandRuntime, int>.Lift(() => Option<int>.None.ToFin())
                 select Done);

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Defect);
    }

    [Fact]
    public async Task EffectRequire_TurnsAnAbsentValueIntoItsCase()
    {
        var outcome = await this.Run(Slash, Privacy.Public, Valid,
            _ => from member in Effect.Require<CommandRuntime, int>(None, new NotFound("That member left."))
                 select Done);

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.NotFound);
    }

    [Fact]
    public async Task AFailedDefer_StopsBeforeTheAct_AndIsAlerted()
    {
        this._env.Command.DeferFails = Unavailable.From("Discord down", new HttpRequestException());

        var outcome = await this.Run(Slash, Privacy.Public, Valid);

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Unavailable);
        this._acted.ShouldBeFalse();
        this._env.Command.Calls.ShouldBe([new Deferred(Delivery.Public)]);
        this._env.Alerts.Sent.ShouldHaveSingleItem().Type.ShouldBe("CommandReplyFailure");
    }

    [Fact]
    public async Task AFailedReply_KeepsTheActsWork_AndSkipsTheLog()
    {
        this._env.Command.ReplyFails = new NotFound("interaction expired");

        var outcome = await this.Run(Slash, Privacy.Public, Valid);

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Success);
        this._acted.ShouldBeTrue();
        this._env.GuildLog.Sent.ShouldBeEmpty();
        var alert = this._env.Alerts.Sent.ShouldHaveSingleItem();
        alert.Type.ShouldBe("CommandReplyFailure");
        alert.Severity.ShouldBe(AlertSeverity.Warning);
    }

    [Fact]
    public async Task AForbiddenSlashReply_IsAReplyFailure_NotALock()
    {
        this._env.Command.ReplyFails = new Forbidden("missing Send Messages");

        (await this.Run(Slash, Privacy.Public, Valid)).ShouldSucceed().ShouldBe(CommandOutcome.Success);

        this._env.Command.Calls.ShouldNotContain(new Reacted("🔒"));
        this._env.Alerts.Sent.ShouldHaveSingleItem().Type.ShouldBe("CommandReplyFailure");
    }

    [Fact]
    public async Task AFailedLogPost_KeepsTheReply()
    {
        this._env.GuildLog.Fails = Unavailable.From("queue closed", new InvalidOperationException());

        (await this.Run(Slash, Privacy.Public, Valid)).ShouldSucceed().ShouldBe(CommandOutcome.Success);

        this._env.Command.Calls[^1].ShouldBeOfType<Replied>();
        this._env.Alerts.Sent.ShouldHaveSingleItem().Type.ShouldBe("CommandReplyFailure");
    }

    [Fact]
    public async Task AFailedRefusal_IsOnlyLogged()
    {
        this._env.Command.RefuseFails = new NotFound("interaction expired");

        (await this.Run(Slash, Privacy.Public, new Invalid("x"))).ShouldSucceed().ShouldBe(CommandOutcome.Invalid);

        this._env.Alerts.Sent.ShouldBeEmpty();
        this._env.Log.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task AFailedGenericReply_StillAlertsOnce()
    {
        this._env.Command.RefuseFails = new NotFound("interaction expired");

        await this.Run(Slash, Privacy.Public, Valid, this.Fails(new Unavailable("down")));

        this._env.Alerts.Sent.ShouldHaveSingleItem().Type.ShouldBe("Unavailable");
    }

    [Theory]
    [InlineData(InvocationKind.Slash, Delivery.Ephemeral)]
    [InlineData(InvocationKind.Text, Delivery.Public)]
    public async Task AFailedPrivacyRead_GetsTheGenericReply_BeforeAnyDefer(InvocationKind kind, Delivery expected)
    {
        var outcome = await this.Run(Slash with { Kind = kind },
            Eff<CommandRuntime, Privacy>.Fail(new Unavailable("settings down")), _ => Valid);

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Unavailable);
        this._env.Command.Calls.ShouldHaveSingleItem().ShouldBeOfType<Refused>().Delivery.ShouldBe(expected);
        this._acted.ShouldBeFalse();
    }

    [Fact]
    public async Task DataDependentPrivacy_IsReadBeforeValidation()
    {
        var order = Seq<string>();

        await this.Run(Slash,
            Eff<CommandRuntime, Privacy>.Lift(() =>
            {
                order = order.Add("privacy");
                return Privacy.Private;
            }),
            _ =>
            {
                order = order.Add("input");
                return Valid;
            });

        order.ShouldBe(["privacy", "input"]);
        this._env.Command.Calls[0].ShouldBe(new Deferred(Delivery.Ephemeral));
    }

    [Fact]
    public async Task ATimeout_IsAnOutage()
    {
        var outcome = await this.Run(Slash, Privacy.Public, Valid, this.Fails(Errors.TimedOut));

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Unavailable);
        this._env.Alerts.Sent.ShouldHaveSingleItem().Type.ShouldBe("Unavailable");
    }

    [Fact]
    public async Task TheLogPost_IsTheRepliesLog()
    {
        await this.Run(Slash, Privacy.Public, Valid);

        this._env.GuildLog.Sent.ShouldHaveSingleItem().ShouldBe(Done.Log.Case.ShouldBeOfType<GuildLogMessage>());
    }
}

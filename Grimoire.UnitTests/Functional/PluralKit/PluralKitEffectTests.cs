// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.PluralKit;
using Grimoire.Features.Shared.PluralKit.Models;
using Grimoire.Functional.Effects;
using Grimoire.Functional.PluralKit;
using Grimoire.Functional.Runtime;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Grimoire.UnitTests.Functional.PluralKit;

/// <summary>
///     Spec 1.5, sections 2 and 3.6: PluralKit&lt;RT&gt;'s window, retry and cap, and the delete log's fallback, against
///     the fake.
/// </summary>
public sealed class PluralKitEffectTests
{
    private static readonly MessageId Original = new(100);
    private static readonly PluralKitMessage Proxied =
        new("200", "100", new PluralKitMessage.Entity("abcde"), new PluralKitMessage.Entity("fghij"));

    private readonly TestEnv _env = new();

    private DateTimeOffset Fresh => this._env.Clock.Now.AddSeconds(-2);

    private Task<Fin<ProxyLookup>> Lookup(DateTimeOffset createdAt) =>
        this._env.Run(PluralKit<GrimoireRuntime>.Lookup(Original, createdAt));

    private Task<Fin<ProxyLookup>> ForDelete() =>
        this._env.Run(DeletedMessageLookup<GrimoireRuntime>.Run(Original, this.Fresh));

    [Fact]
    public async Task AFreshDelete_IsAsked_AndLinked()
    {
        this._env.PluralKit.Knows(Proxied);

        (await this.Lookup(this.Fresh)).ShouldSucceed().ShouldBeOfType<ProxyLookup.ReplacedByProxy>()
            .Link.ProxyMessageId.ShouldBe(new MessageId(200));
        this._env.Journal.Entries.ShouldBe(["pluralkit messages/100"]);
    }

    [Fact]
    public async Task AMessagePluralKitDoesntKnow_IsNotProxied()
        => (await this.Lookup(this.Fresh)).ShouldSucceed().ShouldBeOfType<ProxyLookup.NotProxied>();

    [Theory]
    [InlineData(-60, true)]
    [InlineData(-59, true)]
    [InlineData(-61, false)]
    [InlineData(5, true)]
    public async Task OnlyMessagesUpToAMinuteOld_AreAsked(int ageSeconds, bool asked)
    {
        var result = (await this.Lookup(this._env.Clock.Now.AddSeconds(ageSeconds))).ShouldSucceed();

        if (asked)
            result.ShouldBeOfType<ProxyLookup.NotProxied>();
        else
            result.ShouldBeOfType<ProxyLookup.NotAsked>();
        this._env.PluralKit.Calls.ShouldBe(asked ? 1 : 0);
    }

    [Fact]
    public async Task WhenPluralKitIsOff_NothingIsAsked()
    {
        this._env.PluralKit.IsEnabled = false;

        (await this.Lookup(this.Fresh)).ShouldSucceed().ShouldBeOfType<ProxyLookup.NotAsked>();
        this._env.PluralKit.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task AnOutage_IsRetried_AndThenSucceeds()
    {
        this._env.PluralKit.Knows(Proxied).Fails(FakePluralKitIO.Outage(), times: 2);

        (await this.Lookup(this.Fresh)).ShouldSucceed().ShouldBeOfType<ProxyLookup.ReplacedByProxy>();
        this._env.PluralKit.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task AnOutage_ThatOutlastsTheRetries_IsUnavailable()
    {
        this._env.PluralKit.Fails(FakePluralKitIO.Outage());

        (await this.Lookup(this.Fresh)).ShouldFail().ShouldBeOfType<Unavailable>();
        this._env.PluralKit.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task ARefusal_IsNotRetried()
    {
        this._env.PluralKit.Fails(FakePluralKitIO.Refused());

        (await this.Lookup(this.Fresh)).ShouldFail().ShouldBeOfType<Unavailable>();
        this._env.PluralKit.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task ALookupPastTheCap_IsTooSlow()
    {
        this._env.PluralKit.Cap = TimeSpan.FromMilliseconds(50);
        this._env.PluralKit.Delay = TimeSpan.FromSeconds(5);

        var error = (await this.Lookup(this.Fresh)).ShouldFail();

        error.ShouldBeOfType<Unavailable>();
        PluralKitErrors.ProblemOf(error).ShouldBeSome().ShouldBe(PluralKitProblem.TooSlow);
    }

    [Fact]
    public async Task AnAnswerThatCantBeRead_IsUnavailable()
    {
        this._env.PluralKit.Knows(new PluralKitMessage("200", "100", new PluralKitMessage.Entity("abcde"),
            new PluralKitMessage.Entity(" ")));

        PluralKitErrors.ProblemOf((await this.Lookup(this.Fresh)).ShouldFail()).ShouldBeSome()
            .ShouldBe(PluralKitProblem.Unreadable);
    }

    // ── The delete log's fallback (DeletedMessageLookup) ────────────────────────

    [Fact]
    public async Task ForADelete_AnOutage_IsLoggedAtInformation_AndAnsweredAsNotProxied()
    {
        this._env.PluralKit.Fails(FakePluralKitIO.Outage());

        (await this.ForDelete()).ShouldSucceed().ShouldBeOfType<ProxyLookup.NotProxied>();

        var entry = this._env.Log.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Args.ShouldContain("Outage");
        this._env.Alerts.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task ForADelete_ARefusal_IsAWarning_AndAWarningAlert()
    {
        this._env.PluralKit.Fails(FakePluralKitIO.Refused());

        (await this.ForDelete()).ShouldSucceed().ShouldBeOfType<ProxyLookup.NotProxied>();

        this._env.Log.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Warning);
        var alert = this._env.Alerts.Sent.ShouldHaveSingleItem();
        alert.Severity.ShouldBe(AlertSeverity.Warning);
        alert.Type.ShouldBe("PluralKitLookupFailed");
        alert.Message.ShouldContain("401");
    }

    [Fact]
    public async Task ForADelete_AFullQueue_IsAWarning_WithNoAlert()
    {
        this._env.PluralKit.Fails(PluralKitErrors.QueueFull());

        (await this.ForDelete()).ShouldSucceed().ShouldBeOfType<ProxyLookup.NotProxied>();

        this._env.Log.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Warning);
        this._env.Alerts.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task ForADelete_ADefect_IsAnError_AndAnAlert_ButTheDeleteIsStillLogged()
    {
        this._env.PluralKit.Fails(Error.New(new InvalidOperationException("boom")));

        (await this.ForDelete()).ShouldSucceed().ShouldBeOfType<ProxyLookup.NotProxied>();

        this._env.Log.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Error);
        this._env.Alerts.Sent.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ForADelete_Shutdown_GetsThrough_AsCancelled_NotTooSlow()
    {
        this._env.PluralKit.Delay = TimeSpan.FromSeconds(5);
        using var shutdown = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        using var envIO = EnvIO.New(token: shutdown.Token);

        var result = await DeletedMessageLookup<GrimoireRuntime>.Run(Original, this.Fresh)
            .RunAsync(this._env.Runtime, envIO);

        result.ShouldFail().Is(Errors.Cancelled).ShouldBeTrue();
        this._env.Log.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task ForADelete_ADeletedMembersProxy_IsADebugLine()
    {
        this._env.PluralKit.Knows(new PluralKitMessage("200", "100", null, null));

        (await this.ForDelete()).ShouldSucceed().ShouldBeOfType<ProxyLookup.ReplacedByProxyOfDeletedMember>();

        var entry = this._env.Log.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Debug);
        entry.Args.ShouldBe([100UL, 200UL]);
    }

    [Fact]
    public async Task ForADelete_ASuccess_LogsNothing()
    {
        this._env.PluralKit.Knows(Proxied);

        (await this.ForDelete()).ShouldSucceed().ShouldBeOfType<ProxyLookup.ReplacedByProxy>();
        this._env.Log.Entries.ShouldBeEmpty();
    }
}

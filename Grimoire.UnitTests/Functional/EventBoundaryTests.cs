// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Runtime;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Unit = LanguageExt.Unit;

namespace Grimoire.UnitTests.Functional;

/// <summary>Spec 1.2, section 4: the event table.</summary>
public sealed class EventBoundaryTests
{
    private static readonly GuildId Guild = new(10);
    private readonly TestEnv _env = new();

    private Task<Fin<CommandOutcome>> Run(Eff<GrimoireRuntime, Unit> effect) =>
        this._env.Run(EventBoundary<GrimoireRuntime>.Run("ProbeHandler", Guild, effect));

    private static Eff<GrimoireRuntime, Unit> Failing(Error error) => Eff<GrimoireRuntime, Unit>.Fail(error);

    [Fact]
    public async Task Success_ReportsNothing()
    {
        (await this.Run(Eff<GrimoireRuntime, Unit>.Pure(unit))).ShouldSucceed().ShouldBe(CommandOutcome.Success);

        this._env.Alerts.Sent.ShouldBeEmpty();
        this._env.Log.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task NotFound_IsADebugLine_WithNoAlert()
    {
        (await this.Run(Failing(new NotFound("message deleted")))).ShouldSucceed().ShouldBe(CommandOutcome.NotFound);

        this._env.Alerts.Sent.ShouldBeEmpty();
        this._env.Log.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Debug);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidAndForbidden_AreWarnings(bool invalid)
    {
        Error error = invalid ? new Invalid("too long") : new Forbidden("missing Manage Roles");

        await this.Run(Failing(error));

        var alert = this._env.Alerts.Sent.ShouldHaveSingleItem();
        alert.Severity.ShouldBe(AlertSeverity.Warning);
        alert.Type.ShouldBe("EventHandlerFailure");
        alert.Discriminator.ShouldBe("ProbeHandler");
        alert.GuildId.ShouldBe(Guild);
        this._env.Log.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task AnOutage_IsAnImmediateAlert()
    {
        await this.Run(Failing(Unavailable.From("db down", new TimeoutException())));

        var alert = this._env.Alerts.Sent.ShouldHaveSingleItem();
        alert.Severity.ShouldBe(AlertSeverity.Urgent);
        alert.Type.ShouldBe("Unavailable");
        alert.Exception.ShouldBeOfType<TimeoutException>();
        this._env.Log.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public async Task AThrow_IsADefect_AndTheBoundaryStillSucceeds()
    {
        var outcome = await this.Run(Eff<GrimoireRuntime, Unit>.Lift(() => throw new InvalidOperationException("bug")));

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Defect);
        var alert = this._env.Alerts.Sent.ShouldHaveSingleItem();
        alert.Type.ShouldBe("UnhandledException");
        alert.Severity.ShouldBe(AlertSeverity.Urgent);
    }

    [Fact]
    public async Task AThrowWhileBuildingTheEffect_IsADefect()
    {
        var outcome = await this._env.Run(EventBoundary<GrimoireRuntime>.Run("ProbeHandler", Guild,
            Eff<GrimoireRuntime, Unit>.Pure(unit).Bind(Eff<GrimoireRuntime, Unit> (_) =>
                throw new InvariantViolationException("bad args"))));

        outcome.ShouldSucceed().ShouldBe(CommandOutcome.Defect);
    }
}

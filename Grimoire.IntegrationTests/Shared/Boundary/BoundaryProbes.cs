// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Collections.Concurrent;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.EventArgs;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Runtime;
using Grimoire.Functional.Workers;
using Grimoire.Structs;
using LanguageExt;
using static LanguageExt.Prelude;
using Unit = LanguageExt.Unit;

namespace Grimoire.IntegrationTests.Shared.Boundary;

// Test-only users of the 1.2 boundaries (spec 1.2, OQ-3). No production code runs through them until Phase 3, so these
// prove the wiring inside the real host: registration, DSharpPlus's own events, and the fake Discord REST.

/// <summary>Probe commands. <c>mode</c> picks the outcome: ok, invalid, notfound, unavailable or defect.</summary>
internal sealed class BoundaryProbeCommands(CommandRunner runner)
{
    public const string Ok = "probe ok";

    [Command("probe_public")]
    [Description("Test probe for the command boundary.")]
    public Task PublicAsync(CommandContext ctx, [Parameter("mode")] [Description("The outcome.")] string mode)
        => runner.Run(ctx, _ => Validate(mode), Act);

    [Command("probe_private")]
    [Description("Test probe for a private command.")]
    [PrivateReply]
    public Task PrivateAsync(CommandContext ctx, [Parameter("mode")] [Description("The outcome.")] string mode)
        => runner.Run(ctx, _ => Validate(mode), Act);

    [Command("probe_dm")]
    [Description("Test probe for a command that DMs when run as text.")]
    [PrivateReply(DirectMessageOnText = true)]
    public Task DirectMessageAsync(CommandContext ctx,
        [Parameter("mode")] [Description("The outcome.")] string mode)
        => runner.Run(ctx, _ => Validate(mode), Act);

    private static Validation<Error, string> Validate(string mode) =>
        mode == "invalid" ? new Invalid("probe refused") : Success<Error, string>(mode);

    private static Eff<CommandRuntime, Reply> Act(string mode) =>
        mode switch
        {
            "notfound" => Eff<CommandRuntime, Reply>.Fail(new NotFound("probe not found")),
            "unavailable" => Eff<CommandRuntime, Reply>.Fail(
                Unavailable.From("probe dependency down", new TimeoutException("probe"))),
            "defect" => Eff<CommandRuntime, Reply>.Lift(() => throw new InvalidOperationException("probe bug")),
            _ => Eff<CommandRuntime, Reply>.Pure(Reply.Of(GrimoireColor.Purple, Ok))
        };
}

/// <summary>Fails with a defect on the message "probe event defect".</summary>
internal sealed class BoundaryProbeEventHandler(EventRunner runner) : IEventHandler<MessageCreatedEventArgs>
{
    public const string Trigger = "probe event defect";

    public Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs)
        => eventArgs.Message.Content == Trigger
            ? runner.Run(nameof(BoundaryProbeEventHandler), Optional(eventArgs.Guild).Map(guild => new GuildId(guild.Id)),
                () => Eff<GrimoireRuntime, Unit>.Lift(() => throw new InvalidOperationException("probe event bug")))
            : Task.CompletedTask;
}

/// <summary>What the probe worker does, set by the test. Registered as a singleton by the tests that use it.</summary>
internal sealed class BoundaryProbeWorkerControl
{
    private int _attempts;

    public int Attempts => this._attempts;
    public bool Failing { get; set; } = true;

    public Fin<Unit> Attempt()
    {
        Interlocked.Increment(ref this._attempts);
        return this.Failing ? Fin.Fail<Unit>(new NotFound("probe item gone")) : unit;
    }
}

/// <summary>One item, "probe item", which fails with <c>NotFound</c> while the control says so.</summary>
internal sealed class BoundaryProbeWorker(GrimoireEnv env, BoundaryProbeWorkerControl control)
    : EffectBackgroundService(env, TimeSpan.FromHours(1))
{
    protected override Eff<GrimoireRuntime, Unit> Tick() =>
        this.Items.Item("probe item", Eff<GrimoireRuntime, Unit>.Lift(control.Attempt));
}

/// <summary>Records every alert, in place of the real sender.</summary>
internal sealed class RecordingAlerts : IAlertSender
{
    private readonly ConcurrentQueue<Alert> _sent = new();

    public IReadOnlyList<Alert> Sent => [.. this._sent];

    public void Send(Alert alert) => this._sent.Enqueue(alert);
}

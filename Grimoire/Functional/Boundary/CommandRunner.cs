// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using DSharpPlus.Commands.Processors.TextCommands;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Runtime;
using LanguageExt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Boundary;

/// <summary>
///     Where a command's effect meets DSharpPlus. The <c>[Command]</c> method hands over its input and act steps;
///     nothing runs until this class runs them. It never throws to DSharpPlus (plan C2): <c>CommandErrored</c> is
///     only raised for failures before the command method runs, such as checks and converters.
/// </summary>
public sealed partial class CommandRunner(
    GrimoireEnv env,
    IHostApplicationLifetime lifetime,
    ILogger<CommandRunner> logger)
{
    /// <summary>A command whose privacy is static: <see cref="PrivateReplyAttribute" />, or public without it.</summary>
    public Task Run<TIn>(CommandContext ctx,
        Func<Invocation, Validation<Error, TIn>> input,
        Func<TIn, Eff<CommandRuntime, Reply>> act)
        => this.Execute(ctx, _ => Eff<CommandRuntime, Privacy>.Pure(StaticPrivacy(ctx)), input, act);

    /// <summary>
    ///     A command whose privacy depends on data (TD-11). <paramref name="privacy" /> is read before validation, so a
    ///     private command run as text can refuse to run at all. Such a command doesn't carry the attribute.
    /// </summary>
    public Task Run<TIn>(CommandContext ctx,
        Func<Invocation, Eff<CommandRuntime, Privacy>> privacy,
        Func<Invocation, Validation<Error, TIn>> input,
        Func<TIn, Eff<CommandRuntime, Reply>> act)
        => this.Execute(ctx, privacy, input, act);

    /// <summary>A command with nothing to validate.</summary>
    public Task Run(CommandContext ctx, Func<Invocation, Eff<CommandRuntime, Reply>> act)
        => this.Execute(ctx, _ => Eff<CommandRuntime, Privacy>.Pure(StaticPrivacy(ctx)),
            invocation => Success<Error, Invocation>(invocation), act);

    private async Task Execute<TIn>(CommandContext ctx,
        Func<Invocation, Eff<CommandRuntime, Privacy>> privacy,
        Func<Invocation, Validation<Error, TIn>> input,
        Func<TIn, Eff<CommandRuntime, Reply>> act)
    {
        var invocation = InvocationOf(ctx);
        var effect = CommandBoundary<CommandRuntime>.Run(invocation,
            // Lifted, so a throw while building the privacy effect is a defect, not an exception to DSharpPlus.
            Eff<CommandRuntime, Unit>.Pure(unit).Bind(_ => privacy(invocation)),
            input,
            act);

        using var envIO = EnvIO.New(token: lifetime.ApplicationStopping);
        var result = await effect.RunAsync(new CommandRuntime(env, new LiveCommandIO(ctx)), envIO);

        // The boundary only fails when the run was cancelled before it could report, or a logging call failed.
        var outcome = result.Match(
            Succ: done => done,
            Fail: error =>
            {
                LogBoundaryFailed(logger, Boundary<CommandRuntime>.ExceptionOf(error).OrNull(), invocation.Command,
                    error.Message);
                return Outcome.Classify(error, lifetime.ApplicationStopping.IsCancellationRequested);
            });
        CommandOutcomes.Record(ctx, outcome);
    }

    internal static Privacy StaticPrivacy(CommandContext ctx) =>
        ctx.Command.Attributes.OfType<PrivateReplyAttribute>().FirstOrDefault()?.Privacy ?? Privacy.Public;

    internal static Invocation InvocationOf(CommandContext ctx) =>
        new(ctx.Command.FullName,
            ctx is SlashCommandContext ? InvocationKind.Slash : InvocationKind.Text,
            Optional(ctx.Guild).Map(guild => guild.GetGuildId()),
            ctx.GetChannelId(),
            ctx.User.GetUserId(),
            ctx.Member?.Permissions ?? DiscordPermissions.None,
            ctx switch
            {
                SlashCommandContext slash => slash.Interaction.CreationTimestamp,
                TextCommandContext text => text.Message.CreationTimestamp,
                _ => DateTimeOffset.UtcNow
            });

    [LoggerMessage(LogLevel.Error, "The command boundary for {Command} failed without reporting: {Reason}")]
    private static partial void LogBoundaryFailed(ILogger logger, Exception? exception, string command, string reason);
}

/// <summary>
///     The outcome of each run through <see cref="CommandRunner" />, for <c>CommandHandler</c>'s "finished" log line,
///     which DSharpPlus raises after the command method returns. Entries go with their context.
/// </summary>
public static class CommandOutcomes
{
    private static readonly ConditionalWeakTable<CommandContext, StrongBox<CommandOutcome>> Outcomes = [];

    internal static void Record(CommandContext ctx, CommandOutcome outcome) =>
        Outcomes.AddOrUpdate(ctx, new StrongBox<CommandOutcome>(outcome));

    /// <summary>The recorded outcome; <c>Success</c> for a command that doesn't use the runner yet.</summary>
    public static CommandOutcome Of(CommandContext ctx) =>
        Outcomes.TryGetValue(ctx, out var box) ? box.Value : CommandOutcome.Success;
}

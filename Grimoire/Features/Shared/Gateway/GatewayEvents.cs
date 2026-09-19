// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Gateway;

/// <summary>Logs gateway connection problems and feeds <see cref="GatewayHealth" />.</summary>
internal sealed partial class GatewayEvents(
    GatewayHealth health,
    IAlertSender alerts,
    ILogger<GatewayEvents> logger)
    : IEventHandler<SocketClosedEventArgs>,
        IEventHandler<ZombiedEventArgs>,
        IEventHandler<SessionCreatedEventArgs>,
        IEventHandler<SessionResumedEventArgs>
{
    public Task HandleEventAsync(DiscordClient sender, SocketClosedEventArgs eventArgs)
    {
        health.MarkDisconnected();
        LogSocketClosed(logger, eventArgs.CloseCode, eventArgs.CloseMessage);
        return Task.CompletedTask;
    }

    public Task HandleEventAsync(DiscordClient sender, ZombiedEventArgs eventArgs)
    {
        health.MarkDisconnected();
        LogZombied(logger, eventArgs.Failures);
        return Task.CompletedTask;
    }

    public Task HandleEventAsync(DiscordClient sender, SessionCreatedEventArgs eventArgs)
    {
        var downtime = health.MarkConnected();
        LogSessionCreated(logger, eventArgs.GuildIds.Count, downtime?.TotalSeconds);
        // A new session after downtime means the previous one could not be resumed, so events in the gap were missed.
        if (downtime is { } value)
            alerts.Send(Reconnected("GatewaySessionRecreated",
                $"Gateway reconnected with a new session after {value.TotalSeconds:F0}s; events during the gap were missed."));
        return Task.CompletedTask;
    }

    public Task HandleEventAsync(DiscordClient sender, SessionResumedEventArgs eventArgs)
    {
        var downtime = health.MarkConnected();
        LogSessionResumed(logger, downtime?.TotalSeconds);
        if (downtime is { } value)
            alerts.Send(Reconnected("GatewayResumed", $"Gateway session resumed after {value.TotalSeconds:F0}s."));
        return Task.CompletedTask;
    }

    private static Alert Reconnected(string type, string message) => new()
    {
        Severity = AlertSeverity.Warning, Type = type, Message = message
    };

    [LoggerMessage(LogLevel.Warning, "Gateway socket closed: {CloseCode} {CloseMessage}")]
    private static partial void LogSocketClosed(ILogger logger, int closeCode, string? closeMessage);

    [LoggerMessage(LogLevel.Warning, "Gateway connection zombied after {Failures} failed heartbeats")]
    private static partial void LogZombied(ILogger logger, int failures);

    [LoggerMessage(LogLevel.Information, "Gateway session created for {GuildCount} guilds (down for {DowntimeSeconds}s)")]
    private static partial void LogSessionCreated(ILogger logger, int guildCount, double? downtimeSeconds);

    [LoggerMessage(LogLevel.Information, "Gateway session resumed (down for {DowntimeSeconds}s)")]
    private static partial void LogSessionResumed(ILogger logger, double? downtimeSeconds);
}

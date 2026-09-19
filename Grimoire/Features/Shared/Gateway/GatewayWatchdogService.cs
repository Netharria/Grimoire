// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Gateway;

/// <summary>Raises an urgent alert while the gateway stays down. Repeats are collapsed by the alert cooldown.</summary>
public sealed class GatewayWatchdogService(
    IServiceProvider serviceProvider,
    ILogger<GenericBackgroundService> logger,
    GatewayHealth health,
    IAlertSender alerts)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(30))
{
    public static readonly TimeSpan UrgentAfter = TimeSpan.FromMinutes(2);

    protected override Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (health.DisconnectedFor() is { } downtime && downtime >= UrgentAfter)
            alerts.Send(new Alert
            {
                Severity = AlertSeverity.Urgent,
                Type = "GatewayDisconnected",
                Message = $"Discord gateway has been disconnected for {downtime.TotalMinutes:F0} minutes."
            });

        return Task.CompletedTask;
    }
}

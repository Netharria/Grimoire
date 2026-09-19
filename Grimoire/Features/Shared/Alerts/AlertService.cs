// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Alerts;

public sealed partial class AlertService(
    AlertChannels channels,
    WarningBuffer warningBuffer,
    ILogger<AlertService> logger,
    TimeProvider timeProvider) : BackgroundService, IAlertSender
{
    private const int QueueCapacity = 256;
    private static readonly TimeSpan UrgentCooldown = TimeSpan.FromMinutes(5);

    private readonly AlertCooldown _cooldown = new(UrgentCooldown, timeProvider);

    private readonly Channel<Alert> _channel = Channel.CreateBounded<Alert>(new BoundedChannelOptions(QueueCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropWrite // callers must never wait on alerting
    });

    public void Send(Alert alert)
    {
        if (!this._channel.Writer.TryWrite(alert))
            LogAlertDropped(logger, alert.Type);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await foreach (var alert in this._channel.Reader.ReadAllAsync(cancellationToken))
            try
            {
                await this.ProcessAsync(alert, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never raise an alert about alerting; that can loop.
                LogProcessError(logger, ex, alert.Type);
            }
    }

    private async Task ProcessAsync(Alert alert, CancellationToken cancellationToken)
    {
        switch (alert.Severity)
        {
            case AlertSeverity.Warning:
                if (!warningBuffer.Add(alert))
                    LogAlertDropped(logger, alert.Type);
                return;
            case AlertSeverity.Urgent:
                if (this._cooldown.TryAcquire(alert.Key) is not { } suppressed)
                    return;
                if (await channels.GetAsync(AlertSeverity.Urgent, cancellationToken) is { } channel)
                    await channel.SendMessageAsync(AlertMessageFormatter.FormatUrgent(alert, suppressed));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(alert), alert.Severity, null);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this._channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    [LoggerMessage(LogLevel.Warning, "Dropped alert {AlertType} because the alert queue or warning buffer is full")]
    private static partial void LogAlertDropped(ILogger logger, string alertType);

    [LoggerMessage(LogLevel.Error, "Failed to process alert {AlertType}")]
    private static partial void LogProcessError(ILogger logger, Exception exception, string alertType);
}

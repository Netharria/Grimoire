// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grimoire.Utilities;

public abstract partial class GenericBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<GenericBackgroundService> logger,
    TimeSpan timeSpan) : BackgroundService
{
    private readonly PeriodicTimer _timer = new(timeSpan);


    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {

        var randomTicks = Random.Shared.NextInt64(0, timeSpan.Ticks);
        var timeSpanDelay = TimeSpan.FromTicks(randomTicks);
        LogBackgroundTaskStart(logger, GetType().FullName ?? GetType().Name, timeSpanDelay);

        await Task.Delay(timeSpanDelay, cancellationToken);

        while (await this._timer.WaitForNextTickAsync(cancellationToken))
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                await RunTask(scope.ServiceProvider, cancellationToken);
            }
            catch (Exception ex)
            {
                LogBackgroundTaskError(logger, ex, ex.Message);
            }
    }

    [LoggerMessage(LogLevel.Information, "Starting Background task {type} with delay {timeSpan}")]
    static partial void LogBackgroundTaskStart(ILogger logger, string type, TimeSpan timeSpan);

    [LoggerMessage(LogLevel.Error, "Exception was thrown when running a background task. Message: ({message})")]
    static partial void LogBackgroundTaskError(ILogger logger, Exception ex, string message);

    protected abstract Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

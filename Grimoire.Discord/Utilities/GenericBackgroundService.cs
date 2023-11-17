// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Grimoire.Discord.Utilities;

public abstract class GenericBackgroundService(IServiceProvider serviceProvider, ILogger logger, TimeSpan timeSpan) : BackgroundService
{ 
    protected readonly IServiceProvider _serviceProvider = serviceProvider;
    protected readonly TimeSpan _timeSpan = timeSpan;
    protected readonly PeriodicTimer _timer = new(timeSpan);
    protected readonly ILogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _timer.WaitForNextTickAsync(stoppingToken)
            && !stoppingToken.IsCancellationRequested)
        {
            var stopwatch = Stopwatch.GetTimestamp();
            try
            {
                using var scope = _serviceProvider.CreateScope();
                await this.RunTask(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception was thrown when running a background task. Message: ({message})", ex.Message);
            }
            finally
            {
                var delta = Stopwatch.GetElapsedTime(stopwatch);
                if (delta.TotalMilliseconds > _timeSpan.TotalMilliseconds)
                    this._logger.Warning("Background Task took more than {Frequency} to complete Execution time={ElapsedTime}ms",
                        _timeSpan, delta.TotalMilliseconds);
            }
        }
    }

    protected abstract Task RunTask(CancellationToken stoppingToken);
}

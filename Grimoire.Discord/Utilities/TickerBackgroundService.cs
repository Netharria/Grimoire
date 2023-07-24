// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Grimoire.Discord.Utilities;

public class TickerBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(1));
    private readonly ILogger _logger;

    public TickerBackgroundService(IServiceProvider serviceProvider, ILogger logger)
    {
        this._serviceProvider = serviceProvider;
        this._logger = logger;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _timer.WaitForNextTickAsync(stoppingToken)
            && !stoppingToken.IsCancellationRequested)
        {
            var stopwatch = Stopwatch.GetTimestamp();
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var timeNow = TimeOnly.FromDateTime(DateTime.UtcNow);
                await mediator.Publish(new TimedNotification(timeNow), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception was thrown when running a background task. Message: ({message})", ex.Message);
            }
            finally
            {
                var delta = Stopwatch.GetElapsedTime(stopwatch);
                if (delta.TotalMilliseconds > 1000)
                    this._logger.Warning("Background Task Execution time={ElapsedTime}ms", delta.TotalMilliseconds);
            }
        }
    }
}

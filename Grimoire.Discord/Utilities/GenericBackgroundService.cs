// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Grimoire.Discord.Utilities;

public abstract class GenericBackgroundService(IServiceProvider serviceProvider, ILogger logger, TimeSpan timeSpan) : BackgroundService
{
    private readonly PeriodicTimer _timer = new(timeSpan);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.Information("Starting Background task {Type}", this.GetType().Name);

        while (await this._timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                await this.RunTask(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception was thrown when running a background task. Message: ({message})", ex.Message);
            }
        }
    }

    protected abstract Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken);
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Discord.Utilities
{
    public class TickerBackgroundService : BackgroundService
    {
        private readonly IMediator _mediator;

        public TickerBackgroundService(IMediator mediator)
        {
            this._mediator = mediator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await periodicTimer.WaitForNextTickAsync(stoppingToken))
            {
                var timeNow = TimeOnly.FromDateTime(DateTime.UtcNow);
                await this._mediator.Publish(new TimedNotification(timeNow), stoppingToken);
            }
        }
    }
}

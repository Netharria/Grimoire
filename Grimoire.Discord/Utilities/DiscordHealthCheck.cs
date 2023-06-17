// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Grimoire.Discord.Utilities
{
    public class DiscordHealthCheck : IHealthCheck
    {
        private readonly IDiscordClientService _discordClientService;

        public DiscordHealthCheck(IDiscordClientService discordClientService)
        {
            this._discordClientService = discordClientService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _discordClientService.Client.GetGatewayInfoAsync();
                if (result is not null)
                    return HealthCheckResult.Healthy("Able to connect to discord.");
                return HealthCheckResult.Unhealthy("Unable to get gateway info from discord.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Unable to get gateway info from discord.", ex);
            }
        }
    }
}

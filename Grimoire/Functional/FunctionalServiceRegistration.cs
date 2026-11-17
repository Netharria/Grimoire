// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Data.Dapper;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Data;
using Grimoire.Functional.Discord;
using Grimoire.Functional.Runtime;
using Grimoire.Functional.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Grimoire.Functional;

internal static class FunctionalServiceRegistration
{
    extension(IServiceCollection services)
    {
        /// <summary>
        ///     Registers the language-ext runtime: the live capabilities, the <see cref="GrimoireEnv" /> that bundles
        ///     them, and the command and event runners. All singletons: the capabilities hold no per-request state, and
        ///     the per-command reply capability is built by <see cref="CommandRunner" /> for each run. The database
        ///     capability uses the <see cref="NpgsqlDataSource" /> registered by <c>AddGrimoire</c>, which the EF
        ///     contexts share (spec 1.3, OQ-1). <see cref="ITelemetryIO" /> takes the <c>ActivitySource</c> and
        ///     <see cref="GrimoireMetrics" /> that <c>AddTelemetry</c> registers (spec 1.6). <see cref="IPluralKitIO" /> comes from <c>AddPluralKit</c>, which reads its
        ///     settings (spec 1.5).
        /// </summary>
        public IServiceCollection AddEffectRuntime()
        {
            DapperTypeHandlers.Register();
            return services
                .AddSingleton<ITimeIO, LiveTimeIO>()
                .AddSingleton<ILogIO, LiveLogIO>()
                .AddSingleton<IAlertIO, LiveAlertIO>()
                .AddSingleton<IGuildLogIO, LiveGuildLogIO>()
                .AddSingleton<ITelemetryIO, LiveTelemetryIO>()
                .AddSingleton<IDatabaseIO, NpgsqlDatabaseIO>()
                .AddSingleton<IDiscordIO, LiveDiscordIO>()
                .AddSingleton<IGuildIssueIO, LiveGuildIssueIO>()
                .AddSingleton(provider => new GrimoireEnv(
                    provider.GetRequiredService<ITimeIO>(),
                    provider.GetRequiredService<ILogIO>(),
                    provider.GetRequiredService<IAlertIO>(),
                    provider.GetRequiredService<IGuildLogIO>(),
                    provider.GetRequiredService<ITelemetryIO>(),
                    provider.GetRequiredService<IDatabaseIO>(),
                    provider.GetRequiredService<IDiscordIO>(),
                    provider.GetRequiredService<IGuildIssueIO>(),
                    provider.GetRequiredService<IPluralKitIO>()))
                .AddSingleton<CommandRunner>()
                .AddSingleton<EventRunner>();
        }
    }
}

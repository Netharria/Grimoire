// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging;
using Grimoire.Core.Features.Shared.PipelineBehaviors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Core
{
    public static class CoreServiceRegistration
    {
        public static IServiceCollection AddCoreServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<GrimoireDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("Grimoire")),
                    ServiceLifetime.Transient)
                .AddSingleton<IInviteService, InviteService>()
                .AddTransient<IGrimoireDbContext, GrimoireDbContext>()
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestTimingBehavior<,>))
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(ErrorLoggingBehavior<,>));
            return services;
        }
    }
}

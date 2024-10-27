// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using EntityFramework.Exceptions.PostgreSQL;
using Grimoire.Features.Logging.UserLogging;
using Grimoire.Features.Shared.PipelineBehaviors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire;

public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Grimoire");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var section = configuration.GetSection("ConnectionProperties");
            var hostname = section["Hostname"];
            var port = section["Port"];
            var dbName = section["DbName"];
            var username = section["Username"];
            var password = section["Password"];
            connectionString =
                $"Host={hostname}; Port={port}; Database={dbName}; Username={username}; Password={password}; SSL Mode=Require; Trust Server Certificate=true; Include Error Detail=true";
        }

        services.AddDbContextFactory<GrimoireDbContext>(options =>
                options.UseNpgsql(connectionString,
                        npgsqlOptionsAction => npgsqlOptionsAction.EnableRetryOnFailure())
                    .UseExceptionProcessor())
            .AddSingleton<IInviteService, InviteService>()
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestTimingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(ErrorLoggingBehavior<,>));
        return services;
    }
}

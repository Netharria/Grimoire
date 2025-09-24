// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using EntityFramework.Exceptions.PostgreSQL;
using Grimoire.Settings.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Settings;

public static class SettingsServiceRegistration
{
    public static IServiceCollection AddSettingsServices(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<SettingsDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsqlOptionsAction => npgsqlOptionsAction.EnableRetryOnFailure())
                .UseExceptionProcessor());
        services.AddSingleton<SettingsModule>();
        return services;
    }
}

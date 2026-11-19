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
using Npgsql;

namespace Grimoire.Settings;

public static class SettingsServiceRegistration
{
    /// <summary>
    ///     Registers the settings context over the <see cref="NpgsqlDataSource" /> the host registers, so it shares
    ///     the bot's one connection pool.
    /// </summary>
    public static IServiceCollection AddSettingsServices(this IServiceCollection services)
    {
        services.AddPooledDbContextFactory<SettingsDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(),
                    npgsqlOptionsAction => npgsqlOptionsAction.EnableRetryOnFailure())
                .UseExceptionProcessor());
        services.AddSingleton<SettingsModule>();

        return services;
    }
}

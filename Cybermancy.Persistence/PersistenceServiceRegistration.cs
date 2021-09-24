// -----------------------------------------------------------------------
// <copyright file="PersistenceServiceRegistration.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Persistence
{
    using Cybermancy.Core.Contracts.Persistence;
    using Cybermancy.Persistence.Repositories;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public static class PersistenceServiceRegistration
    {
        public static IServiceCollection AddPersistenceServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<CybermancyDbContext>(options =>
                options.UseMySql(
                    configuration.GetConnectionString("CybermancyConnectionString"),
                    ServerVersion.AutoDetect(configuration.GetConnectionString("CybermancyConnectionString")))
                .UseLazyLoadingProxies()
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors());

            services.AddScoped(typeof(IAsyncRepository<>), typeof(BaseRepository<>));
            services.AddScoped(typeof(IAsyncIdRepository<>), typeof(BaseIdRepository<>));
            services.AddScoped<IRewardRepository, RewardRepository>();
            services.AddScoped<IUserLevelRepository, UserLevelRepository>();

            return services;
        }
    }
}
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cybermancy.Persistence
{
    public static class PersistenceServiceRegistration
    {
        public static IServiceCollection AddPersistenceServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<CybermancyDbContext>(options =>
                options.UseMySql(configuration.GetConnectionString("CybermancyConnectionString"), 
                ServerVersion.AutoDetect(configuration.GetConnectionString("CybermancyConnectionString")))
                .UseLazyLoadingProxies()
#if DEBUG
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
#endif
                );

            services.AddScoped(typeof(IAsyncRepository<>), typeof(BaseRepository<>));
            services.AddScoped(typeof(IAsyncIdRepository<>), typeof(BaseIdRepository<>));
            services.AddScoped<IRewardRepository, RewardRepository>();
            services.AddScoped<IUserLevelRepository, UserLevelRepository>();

            return services;
        }
    }
}
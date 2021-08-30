using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Persistance.Repositories;

namespace Cybermancy.Persistance
{
    public static class PersistenceServiceRegistration
    {
        public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<CybermancyDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("TechnomancyConnectionString")));

            services.AddScoped(typeof(IAsyncRepository<>), typeof(BaseRepository<>));
            services.AddScoped(typeof(IAsyncIdRepository<>), typeof(BaseIdRepository<>));

            return services;
        }
    }
}

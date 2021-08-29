using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Technomancy.Core.Contracts.Persistence;
using Technomancy.Persistance.Repositories;

namespace Technomancy.Persistance
{
    public static class PersistenceServiceRegistration
    {
        public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<TechnomancyDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("TechnomancyConnectionString")));

            services.AddScoped(typeof(IAsyncRepository<>), typeof(BaseRepository<>));
            services.AddScoped(typeof(IAsyncIdRepository<>), typeof(BaseIdRepository<>));

            return services;
        }
    }
}

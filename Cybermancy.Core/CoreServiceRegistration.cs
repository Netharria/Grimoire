using System.Reflection;
using Cybermancy.Core.Contracts.Persistance;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cybermancy.Core
{
    public static class CoreServiceRegistration
    {
        public static IServiceCollection AddCoreServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<CybermancyDbContext>(options =>
                options.UseMySql(
                    configuration.GetConnectionString("CybermancyConnectionString"),
                    ServerVersion.AutoDetect(configuration.GetConnectionString("CybermancyConnectionString")))
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors())
                .AddScoped<ICybermancyDbContext, CybermancyDbContext>()
                .AddMediatR(Assembly.GetExecutingAssembly());
            return services;
        }
    }
}

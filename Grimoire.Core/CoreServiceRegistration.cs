using Grimoire.Core.Features.Logging;
using Grimoire.Core.Features.Shared.PipelineBehaviors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
                    configuration.GetConnectionString("GrimoireConnectionString"))
                    /*.EnableDetailedErrors().EnableSensitiveDataLogging(false)*/,
                    ServiceLifetime.Transient)

                .AddSingleton<IInviteService, InviteService>()
                .AddTransient<IGrimoireDbContext, GrimoireDbContext>()
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestTimingBehavior<,>))
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(ErrorLoggingBehavior<,>));
            return services;
        }
    }
}

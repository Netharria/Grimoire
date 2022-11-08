using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Features.Logging;
using Cybermancy.Core.Features.Shared.PipelineBehaviors;
using Mediator;
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
                options.UseNpgsql(
                    configuration.GetConnectionString("CybermancyConnectionString"))
                    /*.EnableDetailedErrors().EnableSensitiveDataLogging(false)*/,
                    ServiceLifetime.Transient)

                .AddSingleton<IInviteService, InviteService>()
                .AddTransient<ICybermancyDbContext, CybermancyDbContext>()
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestTimingBehavior<,>))
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(ErrorLoggingBehavior<,>));
            return services;
        }
    }
}

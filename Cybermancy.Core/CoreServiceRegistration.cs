using System.Reflection;
using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Features.Logging;
using Cybermancy.Core.Features.Shared.PipelineBehaviors;
using MediatR;
using MediatR.Pipeline;
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
                    .EnableDetailedErrors().EnableSensitiveDataLogging(false))
                .AddSingleton<IInviteService, InviteService>()
                .AddScoped<ICybermancyDbContext, CybermancyDbContext>()
                .AddMediatR(Assembly.GetExecutingAssembly())
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestTimingBehavior<,>))
                .AddScoped(typeof(IRequestExceptionHandler<,,>), typeof(ErrorLoggingBehavior<,,>));
            return services;
        }
    }
}

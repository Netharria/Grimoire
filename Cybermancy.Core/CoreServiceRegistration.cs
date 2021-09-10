using DSharpPlus;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nefarius.DSharpPlus.Extensions.Hosting;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting;
using OpenTracing;
using OpenTracing.Mock;

namespace Cybermancy.Core
{
    public static class CoreServiceRegistration
    {
        public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ITracer>(provider => new MockTracer());
            services.AddDiscord(options =>
                    {
                        options.Token = configuration["token"];
                        options.TokenType = TokenType.Bot;

                        options.AutoReconnect = true;
                        options.MinimumLogLevel = LogLevel.Debug;
                    }
                )
                .AddDiscordHostedService()
                .AddDiscordSlashCommands(extension:
                    (extension =>
                    {
                        extension.RegisterCommands<ExampleSlashCommand>(ulong.Parse(configuration["guildId"]));
                    }));
            return services;
        }
    }
}
using DSharpPlus;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cybermancy.Core
{
    public static class CoreServiceRegistration
    {
        public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration configuration)
        {
            var discordConfiguration = new DiscordConfiguration
            {
                Token = configuration["token"],
                TokenType = TokenType.Bot,

                AutoReconnect = true,
                MinimumLogLevel = LogLevel.Debug
            };
            var discordClient = new DiscordClient(discordConfiguration);
            var slash = discordClient.UseSlashCommands();
            slash.RegisterCommands<ExampleSlashCommand>(ulong.Parse(configuration["guildId"]));
            discordClient.ConnectAsync();
            return services;
        }
    }
}
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Core.Services;
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
            services.AddScoped<IChannelService, ChannelService>();
            services.AddScoped<IGuildService, GuildService>();
            services.AddScoped<ILevelSettingsService, LevelSettingsService>();
            services.AddScoped<IRewardService, RewardService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IUserLevelService, UserLevelService>();
            services.AddScoped<IUserService, UserService>();
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
                .AddDiscordSlashCommands((config =>
                    {
                        //How to add services to be dependency injected into slash commmands.
                        config.Services = services;
                    })
                    (extension =>
                    {
                        extension.RegisterCommands<ExampleSlashCommand>(ulong.Parse(configuration["guildId"]));
                    }));
            
            return services;
        }
    }
}
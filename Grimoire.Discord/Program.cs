// See https://aka.ms/new-console-template for more information
using Grimoire.Core;
using Grimoire.Discord;
using Grimoire.Discord.LevelingModule;
using Grimoire.Discord.LoggingModule;
using Grimoire.Discord.ModerationModule;
using Grimoire.Discord.SharedModule;
using DSharpPlus.Interactivity.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nefarius.DSharpPlus.Interactivity.Extensions.Hosting;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting;
using OpenTracing;
using OpenTracing.Mock;
using Serilog;

Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(x =>
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        x.AddConfiguration(configuration);
    })
    .ConfigureLogging((context, x) => x
        .AddSerilog(new LoggerConfiguration().WriteTo.Console().CreateLogger())
        .AddConfiguration(context.Configuration))
    .ConfigureServices((context, services) =>
        services
        .AddCoreServices(context.Configuration)
        .AddSingleton<ITracer>(provider => new MockTracer())
        .AddHttpClient()
        .AddDiscord(options =>
        {
            options.Token = context.Configuration["token"];
            options.TokenType = TokenType.Bot;

            options.AutoReconnect = true;
            options.MinimumLogLevel = LogLevel.Debug;
            options.Intents = DiscordIntents.All;
        })
        .AddDiscordInteractivity(options =>
        {
            options.PaginationBehaviour = PaginationBehaviour.WrapAround;
            options.ResponseBehavior = InteractionResponseBehavior.Ack;
            options.ResponseMessage = "That's not a valid button";
            options.Timeout = TimeSpan.FromMinutes(2);
            options.PaginationDeletion = PaginationDeletion.DeleteMessage;
        })
        .AddDiscordSlashCommands(
            x =>
            {
                if (x is not SlashCommandsConfiguration config) return;
                //Enables dependancy injection for commands
                config.Services = services.BuildServiceProvider();
            },
            x =>
            {
                if (x is not SlashCommandsExtension extension) return;
                if(ulong.TryParse(context.Configuration["guildId"], out var guildId))
                {
                    extension.RegisterCommands<ExampleSlashCommand>(guildId);
                    extension.RegisterCommands<LevelCommands>(guildId);
                    extension.RegisterCommands<LeaderboardCommands>(guildId);
                    extension.RegisterCommands<LevelSettingsCommands>(guildId);
                    extension.RegisterCommands<LevelingAdminCommands>(guildId);
                    extension.RegisterCommands<RewardCommands>(guildId);
                    extension.RegisterCommands<LogSettingsCommands>(guildId);
                    extension.RegisterCommands<ModuleCommands>(guildId);
                    extension.RegisterCommands<TrackerCommands>(guildId);
                    extension.RegisterCommands<ModerationSettingsCommands>(guildId);
                    extension.RegisterCommands<MuteAdminCommands>(guildId);
                    extension.RegisterCommands<BanCommands>(guildId);
                    extension.RegisterCommands<SinAdminCommands>(guildId);
                    extension.RegisterCommands<LockCommands>(guildId);
                    extension.RegisterCommands<PublishCommands>(guildId);
                    extension.RegisterCommands<SinLogCommands>(guildId);

                }
                extension.RegisterCommands<EmptySlashCommands>();
            })
        .AddDiscordHostedService()
        .AddMediator(options => options.ServiceLifetime = ServiceLifetime.Transient)
        .AddHostedService<TickerBackgroundService>()
        .BuildServiceProvider()
    )
    .UseConsoleLifetime()
    .Build()
    .Run();

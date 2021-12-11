// See https://aka.ms/new-console-template for more information
using Cybermancy;
using Cybermancy.Core;
using Cybermancy.LevelingModule;
using DSharpPlus;
using DSharpPlus.Interactivity.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nefarius.DSharpPlus.Extensions.Hosting;
using Nefarius.DSharpPlus.Interactivity.Extensions.Hosting;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting;
using OpenTracing;
using OpenTracing.Mock;

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
    .AddConsole()
    .AddConfiguration(context.Configuration))
    .ConfigureServices((context, services) => 
        services
        .AddCoreServices(context.Configuration)
        .AddSingleton<ITracer>(provider => new MockTracer())
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
            options.AckPaginationButtons = true;
        })
        .AddDiscordSlashCommands(
            config =>
                // How to add services to be dependency injected into slash commands.
                config.Services = services.BuildServiceProvider(),
            extension =>
            {
                extension.RegisterCommands<ExampleSlashCommand>(ulong.Parse(context.Configuration["guildId"]));
                extension.RegisterCommands<EmptySlashCommands>();
                extension.RegisterCommands<LevelCommands>(ulong.Parse(context.Configuration["guildId"]));
                extension.RegisterCommands<LeaderboardCommands>(ulong.Parse(context.Configuration["guildId"]));
                extension.RegisterCommands<SettingsCommands>(ulong.Parse(context.Configuration["guildId"]));
                extension.RegisterCommands<LevelingAdminCommands>(ulong.Parse(context.Configuration["guildId"]));
            })
        .AddDiscordHostedService()
    )
    .UseConsoleLifetime()
    .Build()
    .Run();

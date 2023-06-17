// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Interactivity.Enums;
using Grimoire.Core;
using Grimoire.Discord;
using Grimoire.Discord.LevelingModule;
using Grimoire.Discord.LoggingModule;
using Grimoire.Discord.ModerationModule;
using Grimoire.Discord.SharedModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nefarius.DSharpPlus.Interactivity.Extensions.Hosting;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting;
using Serilog;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(x =>
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        x.AddConfiguration(configuration);
    })
    .UseSerilog((context, services, logConfig)
        => logConfig
        .ReadFrom.Configuration(context.Configuration))
    .ConfigureServices((context, services) =>
        services
        .AddCoreServices(context.Configuration)
        .AddHttpClient()
        .AddTransient<IDiscordImageEmbedService, DiscordImageEmbedService>()
        .AddDiscord(options =>
        {
            options.Token = context.Configuration["token"];
            options.TokenType = TokenType.Bot;
            options.LoggerFactory = new LoggerFactory().AddSerilog();
            options.AutoReconnect = true;
            options.MinimumLogLevel = LogLevel.Debug;
            options.Intents = DiscordIntents.All;
            options.LogUnknownEvents = false;
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
                if (ulong.TryParse(context.Configuration["guildId"], out var guildId))
                {
                    extension.RegisterCommands<EmptySlashCommands>(guildId);
                }
                //Shared
                extension.RegisterCommands<ModuleCommands>();
                extension.RegisterCommands<ModLogSettings>();
                extension.RegisterCommands<PurgeCommands>();
                //Leveling
                extension.RegisterCommands<LevelCommands>();
                extension.RegisterCommands<LeaderboardCommands>();
                extension.RegisterCommands<LevelSettingsCommands>();
                extension.RegisterCommands<LevelingAdminCommands>();
                extension.RegisterCommands<RewardCommands>();
                extension.RegisterCommands<IgnoreCommands>();

                //Logging
                extension.RegisterCommands<LogSettingsCommands>();
                extension.RegisterCommands<TrackerCommands>();

                //Moderation
                extension.RegisterCommands<ModerationSettingsCommands>();
                extension.RegisterCommands<MuteAdminCommands>();
                extension.RegisterCommands<BanCommands>();
                extension.RegisterCommands<SinAdminCommands>();
                extension.RegisterCommands<LockCommands>();
                extension.RegisterCommands<PublishCommands>();
                extension.RegisterCommands<SinLogCommands>();
                extension.RegisterCommands<MuteCommands>();
                extension.RegisterCommands<WarnCommands>();
            })
        .AddDiscordHostedService()
        .AddMediator(options => options.ServiceLifetime = ServiceLifetime.Transient)
        .AddHostedService<TickerBackgroundService>()
        .BuildServiceProvider()
    )
    .UseConsoleLifetime()
    .Build();
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GrimoireDbContext>();
    db.Database.Migrate();
}
host.Run();

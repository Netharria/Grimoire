// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.RateLimiting;
using DSharpPlus.Interactivity.Enums;
using Grimoire.Core;
using Grimoire.Core.Features.SpamModule;
using Grimoire.Discord;
using Grimoire.Discord.CustomCommandsModule;
using Grimoire.Discord.LevelingModule;
using Grimoire.Discord.LoggingModule;
using Grimoire.Discord.ModerationModule;
using Grimoire.Discord.PluralKit;
using Grimoire.Discord.SharedModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Nefarius.DSharpPlus.Interactivity.Extensions.Hosting;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting;
using Serilog;

var rateLimiter = new SlidingWindowRateLimiter(
    new SlidingWindowRateLimiterOptions
    {
        PermitLimit = 2,
        Window = TimeSpan.FromSeconds(1),
        AutoReplenishment = true,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        SegmentsPerWindow = 1,
        QueueLimit = 0
    });

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
    {
        services
        .AddCoreServices(context.Configuration)
        .AddScoped<IDiscordImageEmbedService, DiscordImageEmbedService>()
        .AddScoped<IDiscordAuditLogParserService, DiscordAuditLogParserService>()
        .AddScoped<IPluralkitService, PluralkitService>()
        .AddSingleton<SpamTrackerModule>()
        .AddDiscord(options =>
        {
            options.Token = context.Configuration["token"]!;
            options.LoggerFactory = new LoggerFactory().AddSerilog();
            options.Intents = DiscordIntents.All;
            options.LogUnknownEvents = false;
        })
        .AddDiscordInteractivity(options =>
        {
            options.ResponseBehavior = InteractionResponseBehavior.Ack;
            options.ResponseMessage = "That's not a valid button";
            options.Timeout = TimeSpan.FromMinutes(2);
            options.PaginationDeletion = PaginationDeletion.DeleteMessage;
        })
        .AddDiscordSlashCommands(extension:
            x =>
            {
                if (x is not SlashCommandsExtension extension) return;
                if (ulong.TryParse(context.Configuration["guildId"], out var guildId))
                {
                    extension.RegisterCommands<EmptySlashCommands>(guildId);
                }
                //Shared
                extension.RegisterCommands<ModuleCommands>();
                extension.RegisterCommands<GeneralSettingsCommands>();
                extension.RegisterCommands<PurgeCommands>();
                extension.RegisterCommands<UserInfoCommands>();


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

                //Custom Commands
                extension.RegisterCommands<ManageCustomCommands>();
                extension.RegisterCommands<CustomCommands>();
            })
        .AddDiscordHostedService()
        .AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped)
        .AddHostedService<LogBackgroundTasks>()
        .AddHostedService<TrackerBackgroundTasks>()
        .AddHostedService<LockBackgroundTasks>()
        .AddHostedService<MuteBackgroundTasks>()
        .AddMemoryCache()
        .AddHttpClient("Default")
        .AddStandardResilienceHandler();

        services.AddHttpClient("Pluralkit", x =>
        {
            var endpoint = context.Configuration["pluralkitApiEndpoint"];
            var userAgent = context.Configuration["pluralkitUserAgent"];
            var token = context.Configuration["pluralkitToken"];

            ArgumentException.ThrowIfNullOrEmpty(endpoint, nameof(endpoint));

            x.BaseAddress = new Uri(endpoint);
            x.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            x.DefaultRequestHeaders.Add("Authorization", token);
        }).AddStandardResilienceHandler(builder
            => builder.RateLimiter = new HttpRateLimiterStrategyOptions
            {
                Name = $"{nameof(RateLimiter)}",
                RateLimiter = args => rateLimiter
                .AcquireAsync(cancellationToken: args.Context.CancellationToken)
            });
    })
    .UseConsoleLifetime()
    .Build();
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GrimoireDbContext>();
    db.Database.Migrate();
}

AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3Support", false);

host.Run();

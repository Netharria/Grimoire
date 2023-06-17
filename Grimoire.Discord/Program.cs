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
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Nefarius.DSharpPlus.Interactivity.Extensions.Hosting;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddConfiguration(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build());

Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, configuration) =>
        configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

builder.WebHost.UseUrls("http://localhost:24700");

builder.Services
        .AddCoreServices(builder.Configuration)
        .AddHttpClient()
        .AddTransient<IDiscordImageEmbedService, DiscordImageEmbedService>()
        .AddDiscord(options =>
        {
            options.Token = builder.Configuration["token"];
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
            x => { },
            x =>
            {
                if (x is not SlashCommandsExtension extension) return;
                if (ulong.TryParse(builder.Configuration["guildId"], out var guildId))
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
        .AddHostedService<TickerBackgroundService>();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<GrimoireDbContext>("Database")
    .AddCheck<DiscordHealthCheck>("Discord");

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapHealthChecks("/_health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GrimoireDbContext>();
    db.Database.Migrate();
}

app.Run();

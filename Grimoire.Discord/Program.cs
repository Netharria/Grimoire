// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Reflection;
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
using Nefarius.DSharpPlus.CommandsNext.Extensions.Hosting;
using Nefarius.DSharpPlus.Interactivity.Extensions.Hosting;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting;
using Polly;
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
    {
        services
        .AddCoreServices(context.Configuration)
        .AddScoped<IDiscordImageEmbedService, DiscordImageEmbedService>()
        .AddScoped<IDiscordAuditLogParserService, DiscordAuditLogParserService>()
        .AddDiscord(options =>
        {
            options.Token = context.Configuration["token"];
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
        .AddDiscordCommandsNext(x => x.StringPrefixes = new [] { ">" },
        x => x?.RegisterCommands(Assembly.GetExecutingAssembly()))
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
        .AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped)
        .AddHostedService<TickerBackgroundService>()
        .AddMemoryCache()
        .AddHttpClient("Default", x => x.Timeout = TimeSpan.FromSeconds(30))
        .AddTransientHttpErrorPolicy(PolicyBuilder => PolicyBuilder
            .OrResult(msg => msg.StatusCode == HttpStatusCode.NotFound)
            .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        services.AddHttpClient("Pluralkit", x => {
            var endpoint = context.Configuration["pluralkitApiEndpoint"];
            var userAgent = context.Configuration["pluralkitUserAgent"];
            var token = context.Configuration["pluralkitToken"];

            ArgumentException.ThrowIfNullOrEmpty(endpoint, nameof(endpoint));
            ArgumentException.ThrowIfNullOrEmpty(userAgent, nameof(userAgent));
            ArgumentException.ThrowIfNullOrEmpty(token, nameof(token));

            x.BaseAddress = new Uri(endpoint);
            x.DefaultRequestHeaders.UserAgent
                .Add(new System.Net.Http.Headers.ProductInfoHeaderValue(userAgent));
            x.DefaultRequestHeaders.Add("Authorization", token);

            })
        .AddTransientHttpErrorPolicy(PolicyBuilder => PolicyBuilder
            .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
        .AddPolicyHandler(
            Policy.HandleResult<HttpResponseMessage>(x => x.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryForeverAsync((_, response, _) =>
            {
                if(response.Result == default)
                {
                    return TimeSpan.FromSeconds(1);
                }
                return response.Result.Headers.RetryAfter is null or { Delta: null }
                ? TimeSpan.FromSeconds(1)
                : response.Result.Headers.RetryAfter.Delta.Value;
            },
            (_,_,_,_) => Task.CompletedTask)
            .WrapAsync(
                Policy.RateLimitAsync(2, TimeSpan.FromSeconds(1))
                .AsAsyncPolicy<HttpResponseMessage>()));
    })
    .UseConsoleLifetime()
    .Build();
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GrimoireDbContext>();
    db.Database.Migrate();
}
host.Run();

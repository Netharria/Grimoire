// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.RateLimiting;
using DSharpPlus.Extensions;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Net;
using DSharpPlus.Net.Gateway;
using Grimoire;
using Grimoire.Features.Leveling.Awards;
using Grimoire.Features.Leveling.Events;
using Grimoire.Features.Leveling.Rewards;
using Grimoire.Features.Leveling.Settings;
using Grimoire.Features.Leveling.UserCommands;
using Grimoire.Features.LogCleanup;
using Grimoire.Features.Logging.MessageLogging.Events;
using Grimoire.Features.Logging.Settings;
using Grimoire.Features.Logging.Trackers;
using Grimoire.Features.Logging.Trackers.Commands;
using Grimoire.Features.Logging.Trackers.Events;
using Grimoire.Features.Logging.UserLogging.Events;
using Grimoire.Features.MessageLogging.Commands;
using Grimoire.Features.Moderation;
using Grimoire.Features.Shared;
using Grimoire.Features.Shared.SharedModule;
using Grimoire.Features.Shared.SpamModule;
using Grimoire.Features.CustomCommands;
using Grimoire.PluralKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
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

AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3Support", false);

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(x =>
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        x.AddConfiguration(configuration);
    })
    .ConfigureServices((context, services) =>
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var token = context.Configuration.GetValue<string>("token")!;
        services
        .AddSerilog(x => x.ReadFrom.Configuration(context.Configuration))
        .AddDiscordClient(
            token: token,
            intents: DiscordIntents.AllUnprivileged | DiscordIntents.GuildMembers | DiscordIntents.MessageContents)
            .Configure<RestClientOptions>(x => { })
            .Configure<GatewayClientOptions>(x => { })
        .AddCoreServices(context.Configuration)
        .AddScoped<IDiscordImageEmbedService, DiscordImageEmbedService>()
        .AddScoped<IDiscordAuditLogParserService, DiscordAuditLogParserService>()
        .AddScoped<IPluralkitService, PluralkitService>()
        .AddSingleton<SpamTrackerModule>()
        .ConfigureEventHandlers(eventHandlerBuilder =>
            eventHandlerBuilder
            //Leveling
                .AddEventHandlers<GainUserXp.EventHandler>()

            //Message Log
                .AddEventHandlers<AddMessageEvent.EventHandler>()
                .AddEventHandlers<DeleteMessageEvent.EventHandler>()
                .AddEventHandlers<BulkMessageDeletedEvent.EventHandler>()
                .AddEventHandlers<UpdateMessageEvent.EventHandler>()
            //Trackers
                .AddEventHandlers<TrackerMessageCreatedEvent>()
                .AddEventHandlers<TrackerMessageUpdateEvent.EventHandler>()
                .AddEventHandlers<TrackerJoinedVoiceChannelEvent>()
            //User Log
                .AddEventHandlers<GuildMemberAddedEvent>()
                .AddEventHandlers<GuildMemberRemovedEvent>()
                .AddEventHandlers<UpdatedAvatarEvent.EventHandler>()
                .AddEventHandlers<UpdatedNicknameEvent.EventHandler>()
                .AddEventHandlers<UpdatedUsernameEvent.EventHandler>())
        .AddInteractivityExtension(new InteractivityConfiguration
        {
            ResponseBehavior = InteractionResponseBehavior.Ack,
            ResponseMessage = "That's not a valid button",
            Timeout = TimeSpan.FromMinutes(2),
            PaginationDeletion = PaginationDeletion.DeleteMessage
        }).AddSlashCommandsExtension(options =>
        {
            if (ulong.TryParse(context.Configuration["guildId"], out var guildId))
            {
                //options.RegisterCommands<EmptySlashCommands>(guildId);
                options.RegisterCommands<CustomCommandSettings>(guildId);
                options.RegisterCommands<GetCustomCommand.Command>(guildId);
            }
            //Shared
            options.RegisterCommands<ModuleCommands>();
            options.RegisterCommands<GeneralSettingsCommands>();
            options.RegisterCommands<PurgeCommands>();
            options.RegisterCommands<UserInfoCommands>();


            ////Leveling
            options.RegisterCommands<AwardUserXp.Command>();
            options.RegisterCommands<ReclaimUserXp.Command>();
            options.RegisterCommands<RewardCommandGroup>();
            options.RegisterCommands<IgnoreCommandGroup>();
            options.RegisterCommands<LevelSettingsCommandGroup>();
            options.RegisterCommands<GetLeaderboard.Command>();
            options.RegisterCommands<GetLevel.Command>();

            ////Logging
            options.RegisterCommands<LogSettingsCommands>();
            options.RegisterCommands<AddTracker.Command>();
            options.RegisterCommands<RemoveTracker.Command>();

            ////Moderation
            //extension.RegisterCommands<ModerationSettingsCommands>();
            //extension.RegisterCommands<Grimoire.ModerationModule.MuteAdminCommands>();
            //extension.RegisterCommands<BanCommands>();
            //extension.RegisterCommands<SinAdminCommands>();
            //extension.RegisterCommands<LockCommands>();
            //extension.RegisterCommands<PublishCommands>();
            //extension.RegisterCommands<SinLogCommands>();
            //extension.RegisterCommands<MuteCommands>();
            //extension.RegisterCommands<WarnCommands>();
            ////Custom Commands
            options.RegisterCommands<CustomCommandSettings>();
            options.RegisterCommands<GetCustomCommand.Command>();
        })
        .AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped)
        .AddHostedService<LogBackgroundTasks>()
        .AddHostedService<TrackerBackgroundTasks>()
        .AddHostedService<LockBackgroundTasks>()
        .AddHostedService<MuteBackgroundTasks>()
        .AddHostedService<DiscordStartService>()
        .AddMemoryCache()
        .AddHttpClient("Default")
        .AddStandardResilienceHandler();
#pragma warning restore CS0618 // Type or member is obsolete

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
        ;
    }).RunConsoleAsync();




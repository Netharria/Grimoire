// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Channels;
using System.Threading.RateLimiting;
using DSharpPlus.Extensions;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using Grimoire;
using Grimoire.Features.CustomCommands;
using Grimoire.Features.Leveling.Events;
using Grimoire.Features.LogCleanup;
using Grimoire.Features.Logging.MessageLogging.Events;
using Grimoire.Features.Logging.Trackers;
using Grimoire.Features.Logging.Trackers.Events;
using Grimoire.Features.Logging.UserLogging.Events;
using Grimoire.Features.Moderation.Ban.Events;
using Grimoire.Features.Moderation.Lock;
using Grimoire.Features.Moderation.Mute;
using Grimoire.Features.Moderation.SpamFilter;
using Grimoire.Features.Shared;
using Grimoire.Features.Shared.Channels;
using Grimoire.Features.Shared.Commands;
using Grimoire.Features.Shared.PluralKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Serilog;
using Channel = System.Threading.Channels.Channel;

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
            .AddJsonFile("appsettings.json", false, true)
            .Build();
        x.AddConfiguration(configuration);
    })
    .ConfigureServices((context, services) =>
    {
        var token = context.Configuration.GetValue<string>("token")!;
        services
            .AddSerilog(x => x.ReadFrom.Configuration(context.Configuration))
            .AddDiscordClient(
                token,
                DiscordIntents.AllUnprivileged | DiscordIntents.GuildMembers | DiscordIntents.MessageContents)
            .AddCoreServices(context.Configuration)
            .AddScoped<IDiscordImageEmbedService, DiscordImageEmbedService>()
            .AddScoped<IDiscordAuditLogParserService, DiscordAuditLogParserService>()
            .AddScoped<IPluralkitService, PluralkitService>()
            .AddSingleton<SpamTrackerModule>()
            .AddSingleton<Channel<PublishToGuildLog>>(
                _ => Channel.CreateUnbounded<PublishToGuildLog>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                }))
            .AddHostedService<PublishToGuildLogProcessor>()
            .ConfigureEventHandlers(eventHandlerBuilder =>
                eventHandlerBuilder
                    .AddEventHandlers<CommandHandler>()
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
                    .AddEventHandlers<UpdatedUsernameEvent.EventHandler>()
                    //Moderation Log
                    .AddEventHandlers<BanAddedEvent>()
                    .AddEventHandlers<BanRemovedEvent>()
                    .AddEventHandlers<UserJoinedWhileMuted.EventHandler>()
            )
            .AddInteractivityExtension(new InteractivityConfiguration
            {
                ResponseBehavior = InteractionResponseBehavior.Ack,
                ResponseMessage = "That's not a valid button",
                Timeout = TimeSpan.FromMinutes(2),
                PaginationDeletion = PaginationDeletion.DeleteMessage
            }).AddCommandsExtension((_, extension) =>
            {
                if (ulong.TryParse(context.Configuration["guildId"], out var guildId))
                {
                    extension.AddCommands<GeneralSettingsCommands>(guildId);
                    extension.AddCommands<PurgeCommands>(guildId);
                    extension.AddCommands<UserInfoCommands>(guildId);
                    extension.AddCommands<CustomCommandSettings>(guildId);
                    extension.AddCommands<GetCustomCommand.Command>(guildId);
                }

                //Shared
                extension.AddCommands<ModuleCommands>();


                // Leveling

                // Logging

                // Moderation

                // Custom Commands

                extension.AddCheck<RequireModuleEnabledCheck>();
                extension.AddCheck<RequireUserGuildPermissionsCheck>();
            }, new CommandsConfiguration()
            {
                // UseDefaultCommandErrorHandler = false,
            })
            .AddMediatR(options => options.RegisterServicesFromAssemblyContaining<Program>())
            .AddHostedService<CleanupLogs.BackgroundTask>()
            .AddHostedService<RemoveExpiredTrackers.BackgroundTask>()
            .AddHostedService<LockBackgroundTasks>()
            .AddHostedService<MuteBackgroundTasks>()
            .AddHostedService<DiscordStartService>()
            .AddMemoryCache()
            .AddHttpClient("Default")
            .AddStandardResilienceHandler();

        services.AddHttpClient("Pluralkit", x =>
        {
            var endpoint = context.Configuration["pluralkitApiEndpoint"];
            var userAgent = context.Configuration["pluralkitUserAgent"];
            var pluralkitToken = context.Configuration["pluralkitToken"];

            ArgumentException.ThrowIfNullOrEmpty(endpoint, nameof(endpoint));

            x.BaseAddress = new Uri(endpoint);
            x.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            x.DefaultRequestHeaders.Add("Authorization", pluralkitToken);
        }).AddStandardResilienceHandler(builder
            => builder.RateLimiter = new HttpRateLimiterStrategyOptions
            {
                Name = $"{nameof(RateLimiter)}",
                RateLimiter = args => rateLimiter
                    .AcquireAsync(cancellationToken: args.Context.CancellationToken)
            });
    }).RunConsoleAsync();

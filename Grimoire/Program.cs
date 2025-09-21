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
using EntityFramework.Exceptions.PostgreSQL;
using Grimoire;
using Grimoire.Features.CustomCommands;
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
using Grimoire.Features.Logging.UserLogging;
using Grimoire.Features.Logging.UserLogging.Events;
using Grimoire.Features.Moderation.Ban.Commands;
using Grimoire.Features.Moderation.Ban.Events;
using Grimoire.Features.Moderation.Lock;
using Grimoire.Features.Moderation.Lock.Commands;
using Grimoire.Features.Moderation.Mute;
using Grimoire.Features.Moderation.Mute.Commands;
using Grimoire.Features.Moderation.PublishSins;
using Grimoire.Features.Moderation.SinAdmin.Commands;
using Grimoire.Features.Moderation.SpamFilter;
using Grimoire.Features.Moderation.SpamFilter.Commands;
using Grimoire.Features.Moderation.Warn;
using Grimoire.Features.Shared;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.Channels.TrackerLog;
using Grimoire.Features.Shared.Commands;
using Grimoire.Features.Shared.Events;
using Grimoire.Features.Shared.PluralKit;
using Grimoire.Settings;
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
            .AddJsonFile("appsettings.json", false, true)
            .Build();
        x.AddConfiguration(configuration);
    })
    .ConfigureServices((context, services) =>
    {
        var token = context.Configuration.GetValue<string>("token")!;
        var connectionString = context.Configuration.GetConnectionString("Grimoire");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var section = context.Configuration.GetSection("ConnectionProperties");
            var hostname = section["Hostname"];
            var port = section["Port"];
            var dbName = section["DbName"];
            var username = section["Username"];
            var password = section["Password"];
            connectionString =
                $"Host={hostname}; Port={port}; Database={dbName}; Username={username}; Password={password}; SSL Mode=Require; Trust Server Certificate=true; Include Error Detail=true";
        }

        services
            .AddSerilog(x => x.ReadFrom.Configuration(context.Configuration))
            .AddDiscordClient(
                token,
                DiscordIntents.AllUnprivileged | DiscordIntents.GuildMembers | DiscordIntents.MessageContents)
            .AddDbContextFactory<GrimoireDbContext>(options =>
                options.UseNpgsql(connectionString,
                        npgsqlOptionsAction => npgsqlOptionsAction.EnableRetryOnFailure())
                    .UseExceptionProcessor())
            .AddSettingsServices(connectionString)
            .AddScoped<IDiscordImageEmbedService, DiscordImageEmbedService>()
            .AddScoped<IDiscordAuditLogParserService, DiscordAuditLogParserService>()
            .AddScoped<IPluralkitService, PluralkitService>()
            .AddSingleton<SpamTrackerModule>()
            .AddSingleton<IInviteService, InviteService>()
            .AddHostedService<GuildLog>()
            .AddHostedService<TrackerLog>()
            .ConfigureEventHandlers(eventHandlerBuilder =>
                eventHandlerBuilder
                    //Custom Commands
                    .AddEventHandlers<TextCustomCommand.EventHandler>()
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
                    .AddEventHandlers<SpamEvents>()
                    //General Events
                    .AddEventHandlers<ChannelAdded.EventHandler>()
                    .AddEventHandlers<ChannelDeleted.EventHandler>()
                    .AddEventHandlers<GuildAdded.EventHandler>()
                    .AddEventHandlers<InviteEvents>()
                    .AddEventHandlers<MemberAdded.EventHandler>()
                    .AddEventHandlers<RoleAdded.EventHandler>()
                    .AddEventHandlers<RoleDeleted.EventHandler>()
                    .AddEventHandlers<UpdateAllGuilds.EventHandler>()
            )
            .AddInteractivityExtension(new InteractivityConfiguration
            {
                ResponseBehavior = InteractionResponseBehavior.Ack,
                ResponseMessage = "That's not a valid button",
                Timeout = TimeSpan.FromMinutes(2),
                PaginationDeletion = PaginationDeletion.DeleteMessage
            }).AddCommandsExtension((_, extension) =>
            {
                if (!ulong.TryParse(context.Configuration["guildId"], out var guildId))
                {
                }

                //Shared
                extension.AddCommands<ModuleCommands>();
                extension.AddCommands<GeneralSettingsCommands>();
                extension.AddCommands<PurgeCommands>();
                extension.AddCommands<UserInfoCommands>();

                // Leveling
                extension.AddCommands<AwardUserXp.Command>();
                extension.AddCommands<ReclaimUserXp.Command>();
                extension.AddCommands<RewardCommandGroup>();
                extension.AddCommands<IgnoreCommandGroup>();
                extension.AddCommands<LevelSettingsCommandGroup>();
                extension.AddCommands<GetLevel.Command>();
                extension.AddCommands<GetLeaderboard.Command>();

                // Logging
                extension.AddCommands<LogSettingsCommands>();

                //Trackers
                extension.AddCommands<AddTracker.Command>();
                extension.AddCommands<RemoveTracker.Command>();

                // Moderation
                extension.AddCommands<AddBanCommand>();
                extension.AddCommands<RemoveBanCommand>();
                extension.AddCommands<LockChannel>();
                extension.AddCommands<UnlockChannel>();
                extension.AddCommands<MuteAdminCommands>();
                extension.AddCommands<MuteUser.Command>();
                extension.AddCommands<UnmuteUser.Command>();
                extension.AddCommands<PublishCommands>();
                extension.AddCommands<ForgetSin.Command>();
                extension.AddCommands<ModSettings>();
                extension.AddCommands<PardonSin.Command>();
                extension.AddCommands<SinLog.Command>();
                extension.AddCommands<UpdateSinReason.Command>();
                extension.AddCommands<SpamFilterOverrideCommands>();
                extension.AddCommands<Warn.Command>();

                // Custom Commands
                extension.AddCommands<CustomCommandSettings>();
                extension.AddCommands<GetCustomCommand.Command>();

                extension.AddCheck<RequireModuleEnabledCheck>();
                extension.AddCheck<RequireUserGuildPermissionsCheck>();
                extension.CommandErrored += (sender, eventArgs)
                    => CommandHandler.HandleEventAsync(sender.Client, eventArgs);
                extension.CommandExecuted += (sender, eventArgs)
                    => CommandHandler.HandleEventAsync(sender.Client, eventArgs);
            }, new CommandsConfiguration { UseDefaultCommandErrorHandler = false })
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

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Core.LevelingModule;
using Cybermancy.Core.Services;
using DSharpPlus;
using DSharpPlus.Interactivity.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nefarius.DSharpPlus.Extensions.Hosting;
using Nefarius.DSharpPlus.Interactivity.Extensions.Hosting;
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
                        extension.RegisterCommands<ExampleSlashCommand>(ulong.Parse(configuration["guildId"]));
                        extension.RegisterCommands<LevelCommands>(ulong.Parse(configuration["guildId"]));
                        extension.RegisterCommands<LeaderboardCommands>(ulong.Parse(configuration["guildId"]));
                        extension.RegisterCommands<SettingsCommands>(ulong.Parse(configuration["guildId"]));
                        extension.RegisterCommands<LevelingAdminCommands>(ulong.Parse(configuration["guildId"]));

                    })
                .AddDiscordHostedService();

            return services;
        }
    }
}

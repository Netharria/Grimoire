// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.IO;
using Grimoire.Features.Shared.PipelineBehaviors;
using Grimoire.Features.Shared.SpamModule;
using Grimoire.Features.UserLogging;
using Grimoire.PluralKit;
using Grimoire.Utilities;
using Mediator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Serilog;

namespace Grimoire.Test.Unit;
public class HostBuilder<T>(GrimoireCoreFactory grimoireCoreFactory) : WebApplicationFactory<Program>
{
    private readonly GrimoireCoreFactory _grimoireCoreFactory = grimoireCoreFactory;

    protected override IHostBuilder? CreateHostBuilder()
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureAppConfiguration((config) =>
        {
            var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
            config.AddConfiguration(configuration);
        })
        .UseSerilog((context, services, logConfig)
        => logConfig
        .ReadFrom.Configuration(context.Configuration))
        .ConfigureServices((context, services) => services
            .AddScoped<IDiscordImageEmbedService, DiscordImageEmbedService>()
            .AddScoped<IDiscordAuditLogParserService, DiscordAuditLogParserService>()
            .AddScoped<IPluralkitService, PluralkitService>()
            .AddSingleton<SpamTrackerModule>()
            .AddDbContextFactory<GrimoireDbContext>(options =>
                options.UseNpgsql(this._grimoireCoreFactory.ConnectionString))
            .AddSingleton<IInviteService, InviteService>()
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestTimingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(ErrorLoggingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(IgnoreDuplicateKeyError<,>))
            .AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped)
            )
        .ConfigureWebHostDefaults(b => b.Configure(app => { })); ;
        return builder;
    }
}

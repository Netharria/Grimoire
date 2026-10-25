// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
    .ConfigureServices((context, services) => services.AddGrimoire(context.Configuration))
    .RunConsoleAsync();

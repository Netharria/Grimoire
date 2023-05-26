// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.MigrationTool.MigrationServices;
using Microsoft.Extensions.Configuration;
using Serilog;

var directory = Directory.GetCurrentDirectory();

var configuration = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .WriteTo.Console().CreateLogger();

await LumberjackMigrationService.MigrateLumberJackDatabaseAsync(configuration);

Log.CloseAndFlush();

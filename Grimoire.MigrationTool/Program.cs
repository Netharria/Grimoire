// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.MigrationTool.Domain;
using Grimoire.MigrationTool.MigrationServices;
using Serilog;



Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(GrimoireDBContextBuilder.Configuration)
                .CreateLogger();

using (var lumberjackDbContext = new LumberjackDbContext())
{
    var lumberjackMigrationTool = new LumberjackMigrationService(lumberjackDbContext);
    await lumberjackMigrationTool.MigrateLumberJackDatabaseAsync();
}

using (var anubisDbContext = new AnubisDbContext())
{
    var anubisMigrationTool = new AnubisMigrationService(anubisDbContext);
    await anubisMigrationTool.MigrateAnubisDatabaseAsync();
}

using (var fuzzyDbContext = new FuzzyDbContext())
{
    var fuzzyMigrationTool = new FuzzyMigrationService(fuzzyDbContext);
    await fuzzyMigrationTool.MigrateFuzzyDatabaseAsync();
}

Log.CloseAndFlush();

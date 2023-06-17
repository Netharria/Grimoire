// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Grimoire.MigrationTool.Domain
{
    public static class GrimoireDBContextBuilder
    {
        public static readonly IConfigurationRoot Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        public static GrimoireDbContext GetGrimoireDbContext()
        {

            var connectionString =
                    Configuration.GetConnectionString("Grimoire");

            return new GrimoireDbContext(
                new DbContextOptionsBuilder<GrimoireDbContext>()
                .UseNpgsql(connectionString)
                .UseLoggerFactory(new LoggerFactory().AddSerilog())
                .Options);
        }
    }
}

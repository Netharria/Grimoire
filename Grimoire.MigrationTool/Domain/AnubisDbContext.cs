// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.MigrationTool.Domain.Anubis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Grimoire.MigrationTool.Domain
{
    public class AnubisDbContext : DbContext
    {

        public string? DbPath;

        public AnubisDbContext()
        {
            this.DbPath = Environment.GetEnvironmentVariable("AnubisPath");
        }
        public DbSet<IgnoredChannels> IgnoredChannels { get; set; }
        public DbSet<IgnoredRoles> IgnoredRoles { get; set; }
        public DbSet<LevelSettings> LevelSettings { get; set; }
        public DbSet<Rewards> Rewards { get; set; }
        public DbSet<UserLevels> UserLevels { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite($"Data Source={this.DbPath}")
                .UseLoggerFactory(new LoggerFactory().AddSerilog());
    }
}

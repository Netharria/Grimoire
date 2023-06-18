// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.MigrationTool.Domain.Lumberjack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Grimoire.MigrationTool.Domain;

public class LumberjackDbContext : DbContext
{
    public string? DbPath;

    public LumberjackDbContext()
    {
        this.DbPath = Environment.GetEnvironmentVariable("LumberjackPath");
    }
    public DbSet<AttachmentUrl> AttachmentUrls { get; set; }
    public DbSet<LogChannelSettings> LogChannels { get; set; }
    public DbSet<LumberjackMessage> LumberjackMessages { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Tracker> Trackers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite($"Data Source={this.DbPath}")
            .UseLoggerFactory(new LoggerFactory().AddSerilog());

}

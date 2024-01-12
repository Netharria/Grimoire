// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.MigrationTool.Domain.Fuzzy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Grimoire.MigrationTool.Domain;

internal sealed class FuzzyDbContext : DbContext
{
    public string? DbPath;

    public FuzzyDbContext()
    {
        this.DbPath = Environment.GetEnvironmentVariable("FuzzyPath");
    }
    public DbSet<Infraction> Infractions { get; set; }
    public DbSet<Lock> Locks { get; set; }
    public DbSet<ModerationSettings> ModerationSettings { get; set; }
    public DbSet<Mute> MuteSettings { get; set; }
    public DbSet<Pardon> Pardons { get; set; }
    public DbSet<PublishedMessage> PublishedMessages { get; set; }
    public DbSet<ThreadLock> ThreadLocks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite($"Data Source={this.DbPath}")
            .UseLoggerFactory(new LoggerFactory().AddSerilog());
}

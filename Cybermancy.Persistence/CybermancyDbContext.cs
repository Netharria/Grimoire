// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Persistence
{
    public class CybermancyDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CybermancyDbContext"/> class.
        /// </summary>
        /// <param name="options"></param>
        public CybermancyDbContext(DbContextOptions<CybermancyDbContext> options)
            : base(options)
        {
        }

        public DbSet<Attachment> Attachments { get; set; }

        public DbSet<Channel> Channels { get; set; }

        public DbSet<Guild> Guilds { get; set; }

        public DbSet<GuildLevelSettings> GuildLevelSettings { get; set; }

        public DbSet<GuildLogSettings> GuildLogSettings { get; set; }

        public DbSet<GuildModerationSettings> GuildModerationSettings { get; set; }

        public DbSet<Lock> Locks { get; set; }

        public DbSet<Message> Messages { get; set; }

        public DbSet<Mute> Mutes { get; set; }

        public DbSet<OldLogMessage> OldLogMessages { get; set; }

        public DbSet<Pardon> Pardons { get; set; }

        public DbSet<PublishedMessage> PublishedMessages { get; set; }

        public DbSet<Reward> Rewards { get; set; }

        public DbSet<Sin> Sins { get; set; }

        public DbSet<Tracker> Trackers { get; set; }

        public DbSet<User> Users { get; set; }

        public DbSet<UserLevel> UserLevels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CybermancyDbContext).Assembly);
    }
}

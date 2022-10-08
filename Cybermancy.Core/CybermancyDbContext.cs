// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core
{
    public class CybermancyDbContext : DbContext, ICybermancyDbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CybermancyDbContext"/> class.
        /// </summary>
        /// <param name="options"></param>
        public CybermancyDbContext(DbContextOptions<CybermancyDbContext> options)
            : base(options)
        {
        }

        public DbSet<Attachment> Attachments
            => this.Set<Attachment>();

        public DbSet<Channel> Channels
            => this.Set<Channel>();

        public DbSet<Guild> Guilds
            => this.Set<Guild>();

        public DbSet<GuildLevelSettings> GuildLevelSettings
            => this.Set<GuildLevelSettings>();

        public DbSet<GuildLogSettings> GuildLogSettings
            => this.Set<GuildLogSettings>();

        public DbSet<GuildModerationSettings> GuildModerationSettings
            => this.Set<GuildModerationSettings>();

        public DbSet<Member> Members
            => this.Set<Member>();

        public DbSet<Lock> Locks
            => this.Set<Lock>();

        public DbSet<Message> Messages
            => this.Set<Message>();

        public DbSet<MessageHistory> MessageHistory
            => this.Set<MessageHistory>();

        public DbSet<Mute> Mutes
            => this.Set<Mute>();

        public DbSet<OldLogMessage> OldLogMessages
            => this.Set<OldLogMessage>();

        public DbSet<NicknameHistory> NicknameHistory
            => this.Set<NicknameHistory>();

        public DbSet<Pardon> Pardons
            => this.Set<Pardon>();

        public DbSet<PublishedMessage> PublishedMessages
            => this.Set<PublishedMessage>();

        public DbSet<Reward> Rewards
            => this.Set<Reward>();

        public DbSet<Role> Roles
            => this.Set<Role>();

        public DbSet<Sin> Sins
            => this.Set<Sin>();

        public DbSet<Tracker> Trackers
            => this.Set<Tracker>();

        public DbSet<User> Users
            => this.Set<User>();
        public DbSet<UsernameHistory> UsernameHistory
            => this.Set<UsernameHistory>();
        public DbSet<XpHistory> XpHistory
            => this.Set<XpHistory>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CybermancyDbContext).Assembly);
    }
}

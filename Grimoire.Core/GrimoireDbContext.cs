// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core;

/// <summary>
/// Initializes a new instance of the <see cref="GrimoireDbContext"/> class.
/// </summary>
/// <param name="options"></param>
public class GrimoireDbContext(DbContextOptions<GrimoireDbContext> options) : DbContext(options), IGrimoireDbContext
{
    public DbSet<Attachment> Attachments
        => this.Set<Attachment>();

    public DbSet<Avatar> Avatars
        => this.Set<Avatar>();

    public DbSet<Channel> Channels
        => this.Set<Channel>();

    public DbSet<Guild> Guilds
        => this.Set<Guild>();

    public DbSet<GuildLevelSettings> GuildLevelSettings
        => this.Set<GuildLevelSettings>();

    public DbSet<GuildUserLogSettings> GuildUserLogSettings
        => this.Set<GuildUserLogSettings>();

    public DbSet<GuildMessageLogSettings> GuildMessageLogSettings
        => this.Set<GuildMessageLogSettings>();

    public DbSet<GuildModerationSettings> GuildModerationSettings
        => this.Set<GuildModerationSettings>();

    public DbSet<IgnoredChannel> IgnoredChannels
        => this.Set<IgnoredChannel>();

    public DbSet<IgnoredMember> IgnoredMembers
        => this.Set<IgnoredMember>();

    public DbSet<IgnoredRole> IgnoredRoles
        => this.Set<IgnoredRole>();

    public DbSet<Lock> Locks
        => this.Set<Lock>();

    public DbSet<Member> Members
        => this.Set<Member>();

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
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(GrimoireDbContext).Assembly);
}

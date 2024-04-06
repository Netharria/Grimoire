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
public sealed class GrimoireDbContext(DbContextOptions<GrimoireDbContext> options) : DbContext(options)
{
    public DbSet<Attachment> Attachments { get; set; } = null!;

    public DbSet<Avatar> Avatars { get; set; } = null!;

    public DbSet<Channel> Channels { get; set; } = null!;

    public DbSet<Guild> Guilds { get; set; } = null!;

    public DbSet<GuildLevelSettings> GuildLevelSettings { get; set; } = null!;

    public DbSet<GuildUserLogSettings> GuildUserLogSettings { get; set; } = null!;

    public DbSet<GuildMessageLogSettings> GuildMessageLogSettings { get; set; } = null!;

    public DbSet<GuildModerationSettings> GuildModerationSettings { get; set; } = null!;

    public DbSet<IgnoredChannel> IgnoredChannels { get; set; } = null!;

    public DbSet<IgnoredMember> IgnoredMembers { get; set; } = null!;

    public DbSet<IgnoredRole> IgnoredRoles { get; set; } = null!;

    public DbSet<Lock> Locks { get; set; } = null!;

    public DbSet<Member> Members { get; set; } = null!;

    public DbSet<Message> Messages { get; set; } = null!;

    public DbSet<MessageLogChannelOverride> MessagesLogChannelOverrides { get; set; } = null!;

    public DbSet<MessageHistory> MessageHistory { get; set; } = null!;

    public DbSet<Mute> Mutes { get; set; } = null!;

    public DbSet<OldLogMessage> OldLogMessages { get; set; } = null!;

    public DbSet<NicknameHistory> NicknameHistory { get; set; } = null!;

    public DbSet<Pardon> Pardons { get; set; } = null!;

    public DbSet<PublishedMessage> PublishedMessages { get; set; } = null!;

    public DbSet<Reward> Rewards { get; set; } = null!;

    public DbSet<Role> Roles { get; set; } = null!;

    public DbSet<Sin> Sins { get; set; } = null!;

    public DbSet<Tracker> Trackers { get; set; } = null!;

    public DbSet<User> Users { get; set; } = null!;

    public DbSet<UsernameHistory> UsernameHistory { get; set; } = null!;

    public DbSet<XpHistory> XpHistory { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(GrimoireDbContext).Assembly);
}

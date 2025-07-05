// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire;

/// <summary>
///     Initializes a new instance of the <see cref="GrimoireDbContext" /> class.
/// </summary>
/// <param name="options"></param>
public sealed class GrimoireDbContext(DbContextOptions<GrimoireDbContext> options) : DbContext(options)
{
    public DbSet<Attachment> Attachments { get; init; } = null!;

    public DbSet<Avatar> Avatars { get; init; } = null!;

    public DbSet<Channel> Channels { get; init; } = null!;

    public DbSet<CustomCommand> CustomCommands { get; init; } = null!;

    public DbSet<CustomCommandRole> CustomCommandsRole { get; init; } = null!;

    public DbSet<Guild> Guilds { get; init; } = null!;

    public DbSet<GuildCommandsSettings> GuildCommandsSettings { get; init; } = null!;

    public DbSet<GuildLevelSettings> GuildLevelSettings { get; init; } = null!;

    public DbSet<GuildUserLogSettings> GuildUserLogSettings { get; init; } = null!;

    public DbSet<GuildMessageLogSettings> GuildMessageLogSettings { get; init; } = null!;

    public DbSet<GuildModerationSettings> GuildModerationSettings { get; init; } = null!;

    public DbSet<IgnoredChannel> IgnoredChannels { get; init; } = null!;

    public DbSet<IgnoredMember> IgnoredMembers { get; init; } = null!;

    public DbSet<IgnoredRole> IgnoredRoles { get; init; } = null!;

    public DbSet<Domain.Lock> Locks { get; init; } = null!;

    public DbSet<Member> Members { get; init; } = null!;

    public DbSet<Message> Messages { get; init; } = null!;

    public DbSet<MessageLogChannelOverride> MessagesLogChannelOverrides { get; init; } = null!;

    public DbSet<MessageHistory> MessageHistory { get; init; } = null!;

    public DbSet<Mute> Mutes { get; init; } = null!;

    public DbSet<NicknameHistory> NicknameHistory { get; init; } = null!;

    public DbSet<OldLogMessage> OldLogMessages { get; init; } = null!;

    public DbSet<Pardon> Pardons { get; init; } = null!;

    public DbSet<ProxiedMessageLink> ProxiedMessages { get; init; }

    public DbSet<PublishedMessage> PublishedMessages { get; init; } = null!;

    public DbSet<Reward> Rewards { get; init; } = null!;

    public DbSet<Role> Roles { get; init; } = null!;

    public DbSet<Sin> Sins { get; init; } = null!;

    public DbSet<SpamFilterOverride> SpamFilterOverrides { get; init; } = null!;

    public DbSet<Tracker> Trackers { get; init; } = null!;

    public DbSet<User> Users { get; init; } = null!;

    public DbSet<UsernameHistory> UsernameHistory { get; init; } = null!;

    public DbSet<XpHistory> XpHistory { get; init; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(GrimoireDbContext).Assembly)
            .HasPostgresExtension("fuzzystrmatch");
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Obsolete;
#pragma warning disable CS0618 // Type or member is obsolete
using Lock = Grimoire.Domain.Obsolete.Lock;

#pragma warning restore CS0618 // Type or member is obsolete

namespace Grimoire;

/// <summary>
///     Initializes a new instance of the <see cref="GrimoireDbContext" /> class.
/// </summary>
/// <param name="options"></param>
public sealed class GrimoireDbContext(DbContextOptions<GrimoireDbContext> options) : DbContext(options)
{
    public DbSet<Attachment> Attachments { get; init; }

    public DbSet<Avatar> Avatars { get; init; }

    [Obsolete("Table To be Dropped Soon.")]
    public DbSet<Channel> Channels { get; init; }

    public DbSet<CustomCommand> CustomCommands { get; init; }

    public DbSet<CustomCommandRole> CustomCommandsRole { get; init; }

    [Obsolete("Table To be Dropped Soon.")]
    public DbSet<Guild> Guilds { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<GuildCommandsSettings> GuildCommandsSettings { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<GuildLevelSettings> GuildLevelSettings { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<GuildUserLogSettings> GuildUserLogSettings { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<GuildMessageLogSettings> GuildMessageLogSettings { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<GuildModerationSettings> GuildModerationSettings { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<IgnoredChannel> IgnoredChannels { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<IgnoredMember> IgnoredMembers { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<IgnoredRole> IgnoredRoles { get; init; }

    public DbSet<LeaderboardView> LeaderboardView { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<Lock> Locks { get; init; }

    [Obsolete("Table to be deleted soon.")]
    public DbSet<Member> Members { get; init; }

    public DbSet<Message> Messages { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<MessageLogChannelOverride> MessagesLogChannelOverrides { get; init; }

    public DbSet<MessageHistory> MessageHistory { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<Mute> Mutes { get; init; }

    public DbSet<NicknameHistory> NicknameHistory { get; init; }

    public DbSet<OldLogMessage> OldLogMessages { get; init; }

    public DbSet<Pardon> Pardons { get; init; }

    public DbSet<ProxiedMessageLink> ProxiedMessages { get; init; }

    public DbSet<PublishedMessage> PublishedMessages { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<Reward> Rewards { get; init; }

    [Obsolete("Table to be deleted soon.")]
    public DbSet<Role> Roles { get; init; }

    public DbSet<Sin> Sins { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<SpamFilterOverride> SpamFilterOverrides { get; init; }


    [Obsolete("Use Settings Module Instead.")]
    public DbSet<Tracker> Trackers { get; init; }

    [Obsolete("Table to be deleted soon.")]
    public DbSet<User> Users { get; init; }

    public DbSet<UsernameHistory> UsernameHistory { get; init; }

    public DbSet<XpHistory> XpHistory { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(GrimoireDbContext).Assembly)
            .HasPostgresExtension("fuzzystrmatch");
}

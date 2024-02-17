// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Grimoire.Core.Contracts.Persistance;

public interface IGrimoireDbContext
{
    public DbSet<Attachment> Attachments { get; }

    public DbSet<Avatar> Avatars { get; }

    public DbSet<Channel> Channels { get; }

    public DbSet<Guild> Guilds { get; }

    public DbSet<GuildLevelSettings> GuildLevelSettings { get; }

    public DbSet<GuildUserLogSettings> GuildUserLogSettings { get; }

    public DbSet<GuildMessageLogSettings> GuildMessageLogSettings { get; }

    public DbSet<GuildModerationSettings> GuildModerationSettings { get; }

    public DbSet<IgnoredChannel> IgnoredChannels { get; }

    public DbSet<IgnoredMember> IgnoredMembers { get; }

    public DbSet<IgnoredRole> IgnoredRoles { get; }

    public DbSet<Lock> Locks { get; }

    public DbSet<Member> Members { get; }

    public DbSet<Message> Messages { get; }

    public DbSet<MessageLogChannelOverride> MessagesLogChannelOverrides { get; }

    public DbSet<MessageHistory> MessageHistory { get; }

    public DbSet<Mute> Mutes { get; }

    public DbSet<NicknameHistory> NicknameHistory { get; }

    public DbSet<OldLogMessage> OldLogMessages { get; }

    public DbSet<Pardon> Pardons { get; }

    public DbSet<PublishedMessage> PublishedMessages { get; }

    public DbSet<Reward> Rewards { get; }

    public DbSet<Role> Roles { get; }

    public DbSet<Sin> Sins { get; }

    public DbSet<Tracker> Trackers { get; }

    public DbSet<User> Users { get; }

    public DbSet<UsernameHistory> UsernameHistory { get; }

    public DbSet<XpHistory> XpHistory { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);

    public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}

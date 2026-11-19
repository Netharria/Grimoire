// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Data.Conventions;

namespace Grimoire;

/// <summary>
///     Initializes a new instance of the <see cref="GrimoireDbContext" /> class.
/// </summary>
/// <param name="options"></param>
public sealed class GrimoireDbContext(DbContextOptions<GrimoireDbContext> options) : DbContext(options)
{
    public DbSet<AlertOccurrence> AlertOccurrences { get; init; }

    public DbSet<Attachment> Attachments { get; init; }

    public DbSet<Avatar> Avatars { get; init; }

    public DbSet<CustomCommand> CustomCommands { get; init; }

    public DbSet<CustomCommandRole> CustomCommandsRole { get; init; }

    public DbSet<CustomCommandUsage> CustomCommandUsages { get; init; }

    public DbSet<LeaderboardView> LeaderboardView { get; init; }

    public DbSet<Message> Messages { get; init; }

    public DbSet<MessageHistoryEntry> MessageHistory { get; init; }

    public DbSet<ServerDisplayNameHistory> ServerDisplayNameHistory { get; init; }

    public DbSet<OldLogMessage> OldLogMessages { get; init; }

    public DbSet<Pardon> Pardons { get; init; }

    public DbSet<PardonReasonHistory> PardonReasonHistory { get; init; }

    public DbSet<ProxiedMessageLink> ProxiedMessages { get; init; }

    public DbSet<Reaction> Reactions { get; init; }

    public DbSet<PublishedMessage> PublishedMessages { get; init; }

    public DbSet<Sin> Sins { get; init; }

    public DbSet<SinReasonHistory> SinReasonHistory { get; init; }

    public DbSet<UsernameHistory> UsernameHistory { get; init; }

    public DbSet<XpHistoryEntry> XpHistory { get; init; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureStronglyTypedIds();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => GrimoireModel.Configure(modelBuilder);
}

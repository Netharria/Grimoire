// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings;

public sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options) : DbContext(options)
{
    internal DbSet<GuildSetting> GuildSettings { get; init; }
    internal DbSet<XpIgnoredItem> XpIgnoredItems { get; init; }
    internal DbSet<ChannelLock> ChannelLocks { get; init; }
    internal DbSet<ThreadLock> ThreadLocks { get; init; }
    internal DbSet<MessageLogChannelOverride> MessagesLogChannelOverrides { get; init; }
    internal DbSet<Mute> Mutes { get; init; }
    internal DbSet<Reward> Rewards { get; init; }
    internal DbSet<SpamFilterOverride> SpamFilterOverrides { get; init; }
    internal DbSet<Tracker> Trackers { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Settings");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SettingsDbContext).Assembly);
    }
}

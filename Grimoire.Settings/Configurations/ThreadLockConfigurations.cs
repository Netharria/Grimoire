// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

[ExcludeFromCodeCoverage]
internal sealed class ThreadLockConfigurations : IEntityTypeConfiguration<ThreadLock>,
    IEntityTypeConfiguration<ThreadLocked>
{
    public void Configure(EntityTypeBuilder<ThreadLock> builder)
    {
        builder.HasKey(e => new { e.ChannelId, e.GuildId, e.SetAt });

        builder.HasDiscriminator<string>("EventType")
            .HasValue<ThreadLocked>("Locked")
            .HasValue<ThreadUnlocked>("Unlocked")
            .IsComplete();

        builder.Property(e => e.Reason)
            .HasConversion(
                r => r == null ? null : r.Value.Value,
                v => v == null ? null : ModerationReason.FromDatabase(v))
            .HasMaxLength(1000);

        builder.Property(e => e.GuildId);
        builder.Property(e => e.ChannelId);
        builder.Property(e => e.ModeratorId);
    }

    // Only open locks, so the expiry poll's cost follows what is in force, not the history (review S-13).
    public void Configure(EntityTypeBuilder<ThreadLocked> builder)
        => builder.HasIndex(x => x.EndTime).HasFilter("\"EventType\" = 'Locked' AND \"ClosedAt\" IS NULL");
}

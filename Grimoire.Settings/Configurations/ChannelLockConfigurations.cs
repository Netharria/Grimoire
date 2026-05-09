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
internal sealed class ChannelLockConfigurations : IEntityTypeConfiguration<ChannelLock>,
    IEntityTypeConfiguration<ChannelLocked>
{
    public void Configure(EntityTypeBuilder<ChannelLock> builder)
    {
        builder.HasKey(e => new { e.ChannelId, e.GuildId, e.SetAt });

        builder.HasDiscriminator<string>("EventType")
            .HasValue<ChannelLocked>("Locked")
            .HasValue<ChannelUnlocked>("Unlocked")
            .IsComplete();

        builder.HasIndex(e => new { e.ChannelId, e.GuildId, e.SetAt })
            .IsDescending(false, false, true);

        builder.Property(e => e.Reason)
            .HasConversion(
                r => r == null ? null : r.Value.Value,
                v => v == null ? null : ModerationReason.FromDatabase(v))
            .HasMaxLength(1000);

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));
        builder.Property(e => e.ChannelId)
            .HasConversion(e => e.Value, value => new ChannelId(value));
        builder.Property(e => e.ModeratorId)
            .HasConversion(e => e.Value, value => new ModeratorId(value));
    }

    public void Configure(EntityTypeBuilder<ChannelLocked> builder)
    {
        builder.HasIndex(x => x.EndTime);

        builder.Property(e => e.PreviouslyAllowed)
            .HasConversion(e => e.Permissions, value => new PreviouslyAllowedPermissions(value));
        builder.Property(e => e.PreviouslyDenied)
            .HasConversion(e => e.Permissions, value => new PreviouslyDeniedPermissions(value));
    }
}

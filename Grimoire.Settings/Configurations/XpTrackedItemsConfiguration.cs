// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

internal sealed class XpTrackedItemsConfiguration : IEntityTypeConfiguration<XpTrackedItem>
{
    public void Configure(EntityTypeBuilder<XpTrackedItem> builder)
    {
        builder.HasKey(nameof(XpTrackedItem.GuildId), "Id", nameof(XpTrackedItem.SetAt));
        builder.Property<ulong>("Id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.HasIndex(nameof(XpTrackedItem.GuildId), "Id", nameof(XpTrackedItem.SetAt))
            .IsDescending(false, false, true);

        builder.Property(e => e.GuildId)
            .ValueGeneratedNever()
            .HasConversion(e => e.Value, value => new GuildId(value))
            .IsRequired();

        builder.Property(e => e.SetBy)
            .HasConversion(e => e.Value, value => new ModeratorId(value));

        builder.Ignore("ChannelId");
        builder.Ignore("RoleId");
        builder.Ignore("UserId");

        builder.HasDiscriminator<string>("Type")
            .HasValue<IgnoredChannel>("IgnoredChannel")
            .HasValue<WatchedChannel>("WatchedChannel")
            .HasValue<IgnoredRole>("IgnoredRole")
            .HasValue<WatchedRole>("WatchedRole")
            .HasValue<IgnoredMember>("IgnoredMember")
            .HasValue<WatchedMember>("WatchedMember")
            .IsComplete();
    }
}

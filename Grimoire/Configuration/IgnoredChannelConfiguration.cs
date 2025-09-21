// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class IgnoredChannelConfiguration : IEntityTypeConfiguration<IgnoredChannel>
{
    public void Configure(EntityTypeBuilder<IgnoredChannel> builder)
    {
        builder.HasKey(e => e.ChannelId);
        builder.Property(e => e.ChannelId)
            .ValueGeneratedNever()
            .IsRequired();
        builder.HasOne(e => e.Channel)
            .WithOne()
            .HasForeignKey<IgnoredChannel>(e => e.ChannelId)
            .IsRequired();
        builder.HasOne(e => e.Guild)
            .WithMany()
            .HasForeignKey(e => e.GuildId)
            .IsRequired();
    }
}

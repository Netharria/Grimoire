// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class CustomCommandConfiguration : IEntityTypeConfiguration<CustomCommand>
{
    public void Configure(EntityTypeBuilder<CustomCommand> builder)
    {
        builder.HasKey(e => new { e.Name, e.GuildId });
        builder.HasOne(e => e.Guild)
            .WithMany()
            .HasForeignKey(e => e.GuildId)
            .IsRequired();
        builder.Property(e => e.Name)
            .HasMaxLength(24);
        builder.Property(e => e.Content)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(e => e.EmbedColor)
            .HasMaxLength(6)
            .IsRequired(false);
    }
}

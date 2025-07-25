// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class MuteConfiguration : IEntityTypeConfiguration<Mute>
{
    public void Configure(EntityTypeBuilder<Mute> builder)
    {
        builder.HasKey(e => e.SinId);
        builder.HasOne(e => e.Sin).WithOne(e => e.Mute)
            .HasForeignKey<Mute>(e => e.SinId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
        builder.HasOne(e => e.Member)
            .WithOne(e => e.ActiveMute)
            .HasForeignKey<Mute>(e => new { e.UserId, e.GuildId })
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.Property(e => e.EndTime).IsRequired();
        builder.HasIndex(x => x.EndTime);
    }
}

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Core.Configuration
{
    [ExcludeFromCodeCoverage]
    public class MuteConfiguration : IEntityTypeConfiguration<Mute>
    {
        public void Configure(EntityTypeBuilder<Mute> builder)
        {
            builder.HasKey(e => e.SinId);
            builder.HasOne(e => e.Sin).WithOne(e => e.Mute)
                .HasForeignKey<Mute>(e => e.SinId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            builder.HasOne(e => e.Member)
                .WithMany(e => e.ActiveMutes)
                .HasForeignKey(e => new { e.UserId, e.GuildId })
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.Property(e => e.EndTime).IsRequired();
        }
    }
}

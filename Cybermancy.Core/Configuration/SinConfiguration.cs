// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Core.Configuration
{
    [ExcludeFromCodeCoverage]
    public class SinConfiguration : IEntityTypeConfiguration<Sin>
    {
        public void Configure(EntityTypeBuilder<Sin> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd();
            builder.Property(e => e.Reason)
                .HasMaxLength(1000);
            builder.HasOne(e => e.GuildUser)
                .WithMany(e => e.UserSins)
                .HasForeignKey(e => new { e.UserId, e.GuildId })
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.Moderator)
                .WithMany(e => e.ModeratedSins)
                .HasForeignKey(e => new { e.ModeratorId, e.GuildId })
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(e => e.Guild)
                .WithMany(e => e.Sins)
                .HasForeignKey(e => e.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.Mute)
                .WithOne(e => e.Sin)
                .IsRequired(false);
            builder.HasOne(e => e.Pardon)
                .WithOne(e => e.Sin)
                .IsRequired(false);
        }
    }
}

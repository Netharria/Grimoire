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
internal sealed class RewardConfiguration : IEntityTypeConfiguration<Reward>
{
    public void Configure(EntityTypeBuilder<Reward> builder)
    {
        builder.HasKey(e => e.RoleId);
        builder.HasOne(e => e.Role)
            .WithOne()
            .HasForeignKey<Reward>(e => e.RoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne(e => e.Guild)
            .WithMany()
            .HasForeignKey(e => e.GuildId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.Property(e => e.RewardMessage)
            .HasMaxLength(4096)
            .IsRequired(false);
        builder.Property(e => e.RewardLevel).IsRequired();
        builder.HasIndex(e => new { e.GuildId, e.RewardLevel });
    }
}

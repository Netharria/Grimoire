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
internal sealed class SinReasonHistoryConfiguration : IEntityTypeConfiguration<SinReasonHistory>
{
    public void Configure(EntityTypeBuilder<SinReasonHistory> builder)
    {
        builder.HasKey(e => new { e.SinId, e.SetAt });

        builder.HasIndex(e => new { e.SinId, e.SetAt })
            .IsDescending(false, true);

        builder.HasOne(e => e.Sin)
            .WithMany(e => e.ReasonHistory)
            .HasForeignKey(e => e.SinId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.SinId)
            .HasConversion(e => e.Value, value => new SinId(value));

        builder.Property(e => e.ModeratorId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ModeratorId(value));
    }
}

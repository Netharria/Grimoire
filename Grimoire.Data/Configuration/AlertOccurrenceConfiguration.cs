// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Grimoire.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class AlertOccurrenceConfiguration : IEntityTypeConfiguration<AlertOccurrence>
{
    public void Configure(EntityTypeBuilder<AlertOccurrence> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.HasIndex(e => e.AlertKey)
            .IsUnique()
            .HasFilter("\"ReportedAt\" IS NULL");
        builder.HasIndex(e => e.LastSeen);

        builder.Property(e => e.AlertKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.AlertType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ExceptionType).HasMaxLength(300);
        builder.Property(e => e.SampleMessage).HasMaxLength(1000).IsRequired();

        builder.Property(e => e.GuildIds)
            .HasConversion(
                new ValueConverter<GuildId[], long[]>(
                    ids => ids.Select(id => (long)id.Value).ToArray(),
                    values => values.Select(v => new GuildId((ulong)v)).ToArray()),
                new ValueComparer<GuildId[]>(
                    (a, b) => a!.SequenceEqual(b!),
                    a => a.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
                    a => a.ToArray()))
            .IsRequired();
    }
}

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
internal sealed class XpHistoryConfiguration : IEntityTypeConfiguration<XpHistoryEntry>
{
    public void Configure(EntityTypeBuilder<XpHistoryEntry> builder)
    {
        builder.HasKey(x => new { x.UserId, x.GuildId, x.TimeOut });

        builder.HasDiscriminator<string>("Type")
            .HasValue<EarnedXp>("Earned")
            .HasValue<AwardedXp>("Awarded")
            .HasValue<ReclaimedXp>("Reclaimed")
            .HasValue<MigratedXp>("Migrated");

        // Map the raw Xp backing column used for DB storage and aggregate queries.
        // Use the typed Xp property on concrete subtypes; use RawXp for cross-subtype LINQ.
        builder.Property(x => x.RawXp)
            .HasColumnName("Xp")
            .IsRequired();

        builder.Property(x => x.TimeOut).IsRequired();

        // For leaderboard queries: GroupBy UserId after filtering GuildId, then Sum(RawXp) and OrderBy
        builder.HasIndex(x => new { x.GuildId, x.RawXp })
            .HasDatabaseName("IX_XpHistory_GuildId_Xp");

        // For user-specific queries: Filter by UserId + GuildId, then aggregate RawXp
        builder.HasIndex(x => new { x.UserId, x.GuildId, x.RawXp })
            .HasDatabaseName("IX_XpHistory_UserId_GuildId_Xp");

        builder.Property(x => x.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));

        builder.Property(x => x.UserId)
            .HasConversion(e => e.Value, value => new UserId(value));
    }
}

[ExcludeFromCodeCoverage]
internal sealed class AwardedXpConfiguration : IEntityTypeConfiguration<AwardedXp>
{
    public void Configure(EntityTypeBuilder<AwardedXp> builder)
    {
        builder.Property(x => x.AwarderId)
            .HasConversion(v => v.Value, v => new ModeratorId(v))
            .HasColumnName("AwarderId");
    }
}

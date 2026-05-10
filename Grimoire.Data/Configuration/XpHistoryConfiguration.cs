// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class XpHistoryConfiguration : IEntityTypeConfiguration<XpHistory>
{
    public void Configure(EntityTypeBuilder<XpHistory> builder)
    {
        builder.HasKey(x => new { x.UserId, x.GuildId, x.TimeOut });
        builder.Property(x => x.Xp)
            .IsRequired();
        builder.Property(x => x.TimeOut)
            .IsRequired();
        builder.Property(x => x.Type)
            .IsRequired();

        // For leaderboard queries: GroupBy UserId after filtering GuildId, then Sum(Xp) and OrderBy
        builder.HasIndex(x => new { x.GuildId, x.Xp })
            .HasDatabaseName("IX_XpHistory_GuildId_Xp");

        // For user-specific queries: Filter by UserId + GuildId, then aggregate Xp
        builder.HasIndex(x => new { x.UserId, x.GuildId, x.Xp })
            .HasDatabaseName("IX_XpHistory_UserId_GuildId_Xp");


        builder.Property(e => e.AwarderId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ModeratorId(value));

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));

        builder.Property(e => e.UserId)
            .HasConversion(e => e.Value, value => new UserId(value));
    }
}

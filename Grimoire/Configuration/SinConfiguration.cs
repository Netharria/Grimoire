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
internal sealed class SinConfiguration : IEntityTypeConfiguration<Sin>
{
    public void Configure(EntityTypeBuilder<Sin> builder)
    {
        builder.HasKey(sin => sin.Id);
        builder.Property(sin => sin.Id)
            .UseIdentityAlwaysColumn();
        builder.Property(sin => sin.Reason)
            .HasMaxLength(1000);
        builder.HasOne(sin => sin.Pardon)
            .WithOne(sin => sin.Sin)
            .HasForeignKey<Pardon>(sin => sin.SinId)
            .IsRequired(false);
        builder.Property(sin => sin.SinOn)
            .HasDefaultValueSql("now()");
        // Primary query pattern: Filter by UserId + GuildId, order by SinOn
        builder.HasIndex(sin => new { sin.UserId, sin.GuildId, sin.SinOn })
            .HasDatabaseName("IX_Sin_UserId_GuildId_SinOn");

        // For moderator stats: GroupBy SinType after filtering ModeratorId + GuildId
        builder.HasIndex(sin => new { sin.ModeratorId, sin.GuildId, sin.SinType })
            .HasDatabaseName("IX_Sin_ModeratorId_GuildId_SinType");

        // For single sin lookups: Filter by Id + GuildId
        builder.HasIndex(sin => new { sin.Id, sin.GuildId })
            .HasDatabaseName("IX_Sin_Id_GuildId");


        builder.Property(e => e.ModeratorId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ModeratorId(value));

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));

        builder.Property(e => e.Id)
            .HasConversion(e => e.Value, value => new SinId(value));
        builder.Property(e => e.UserId)
            .HasConversion(e => e.Value, value => new UserId(value));
    }
}

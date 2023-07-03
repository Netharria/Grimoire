// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Grimoire.Core.Configuration;

public class AvatarConfiguration : IEntityTypeConfiguration<Avatar>
{
    public void Configure(EntityTypeBuilder<Avatar> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(e => e.Id)
            .UseIdentityAlwaysColumn();
        builder.HasOne(x => x.Member)
            .WithMany(x => x.AvatarHistory)
            .HasForeignKey(x => new { x.UserId, x.GuildId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(e => e.FileName)
            .HasMaxLength(2048)
            .IsRequired();
        builder.Property(x => x.Timestamp)
            .HasDefaultValueSql("now()");
        builder.HasIndex(x => new { x.UserId, x.GuildId, x.Timestamp })
            .IsDescending(false, false, true);
    }
}

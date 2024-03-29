// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Core.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class UsernameHistoryConfiguration : IEntityTypeConfiguration<UsernameHistory>
{
    public void Configure(EntityTypeBuilder<UsernameHistory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(e => e.Id)
            .UseIdentityAlwaysColumn();
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.User)
            .WithMany(x => x.UsernameHistories)
            .HasForeignKey(x => x.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Username)
            .HasMaxLength(37)
            .IsRequired();
        builder.Property(x => x.Timestamp)
            .HasDefaultValueSql("now()");
        builder.HasIndex(x => new { x.UserId, x.Timestamp })
            .IsDescending(false, true);
    }
}

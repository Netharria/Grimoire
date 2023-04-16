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
    public class UsernameHistoryConfiguration : IEntityTypeConfiguration<UsernameHistory>
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
                .IsRequired();
            builder.Property(x => x.Username)
                .HasMaxLength(32)
                .IsRequired();
            builder.Property(x => x.Timestamp)
                .HasDefaultValueSql("now()");
        }
    }
}

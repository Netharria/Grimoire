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
internal sealed class UsernameHistoryConfiguration : IEntityTypeConfiguration<UsernameHistory>
{
    public void Configure(EntityTypeBuilder<UsernameHistory> builder)
    {
        builder.HasKey(x => new { x.UserId, x.Timestamp });
        builder.Property(x => x.Username)
            .HasMaxLength(37)
            .IsRequired();
        builder.Property(x => x.Timestamp)
            .HasDefaultValueSql("now()");
    }
}

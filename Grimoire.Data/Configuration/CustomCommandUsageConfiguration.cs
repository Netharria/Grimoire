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
internal sealed class CustomCommandUsageConfiguration : IEntityTypeConfiguration<CustomCommandUsage>
{
    public void Configure(EntityTypeBuilder<CustomCommandUsage> builder)
    {
        builder.HasKey(e => new { e.Name, e.GuildId, e.UserId, e.UsedAt });

        builder.Property(e => e.Name)
            .HasConversion(name => name.Value, value => CustomCommandName.FromDatabase(value))
            .HasMaxLength(24);

        builder.HasIndex(e => new { e.GuildId, e.Name, e.UsedAt });
    }
}

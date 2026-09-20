// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

[ExcludeFromCodeCoverage]
internal sealed class DiagnosticsIgnoredItemConfiguration : IEntityTypeConfiguration<DiagnosticsIgnoredItem>
{
    public void Configure(EntityTypeBuilder<DiagnosticsIgnoredItem> builder)
    {
        // One row per ignored role or member. Removing an entry deletes the row.
        builder.HasKey(nameof(DiagnosticsIgnoredItem.GuildId), "Id");
        builder.Property<ulong>("Id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(e => e.GuildId)
            .ValueGeneratedNever()
            .HasConversion(e => e.Value, value => new GuildId(value))
            .IsRequired();

        builder.Property(e => e.SetBy)
            .HasConversion(e => e.Value, value => new ModeratorId(value));

        builder.Ignore("RoleId");
        builder.Ignore("UserId");

        builder.HasDiscriminator<string>("Type")
            .HasValue<DiagnosticsIgnoredRole>("Role")
            .HasValue<DiagnosticsIgnoredMember>("Member")
            .IsComplete();
    }
}

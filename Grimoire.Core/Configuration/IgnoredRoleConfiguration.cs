// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Core.Configuration;

internal class IgnoredRoleConfiguration : IEntityTypeConfiguration<IgnoredRole>
{
    public void Configure(EntityTypeBuilder<IgnoredRole> builder)
    {
        builder.HasKey(e => e.RoleId);
        builder.Property(e => e.RoleId)
            .ValueGeneratedNever()
            .IsRequired();
        builder.HasOne(e => e.Role)
            .WithOne(e => e.IsIgnoredRole)
            .HasForeignKey<IgnoredRole>(e => e.RoleId)
            .IsRequired(true);
        builder.HasOne(e => e.Guild)
            .WithMany(e => e.IgnoredRoles)
            .HasForeignKey(e => e.GuildId)
            .IsRequired(true);
    }
}

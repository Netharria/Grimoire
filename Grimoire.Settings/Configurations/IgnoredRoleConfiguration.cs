// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

internal sealed class IgnoredRoleConfiguration : IEntityTypeConfiguration<IgnoredRole>
{
    public void Configure(EntityTypeBuilder<IgnoredRole> builder)
    {
        builder.HasKey(e => e.RoleId);
        builder.Property(e => e.RoleId)
            .ValueGeneratedNever()
            .IsRequired();
        builder.Ignore(role => role.Id);


        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));
        builder.Property(e => e.RoleId)
            .HasConversion(e => e.Value, value => new RoleId(value));
    }
}

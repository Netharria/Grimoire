// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class CustomCommandRoleConfiguration : IEntityTypeConfiguration<CustomCommandRole>
{
    public void Configure(EntityTypeBuilder<CustomCommandRole> builder)
    {
        builder.HasKey(e => new { e.CustomCommandName, e.GuildId, e.RoleId });
        builder.HasOne(e => e.CustomCommand)
            .WithMany(e => e.CustomCommandRoles)
            .HasForeignKey(e => new { e.CustomCommandName, e.GuildId })
            .IsRequired();

        builder.Property(e => e.RoleId)
            .HasConversion(e => e.Value, value => new RoleId(value));

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));
    }
}

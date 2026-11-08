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
internal sealed class CustomCommandRoleConfiguration : IEntityTypeConfiguration<CustomCommandRole>
{
    public void Configure(EntityTypeBuilder<CustomCommandRole> builder)
    {
        builder.HasKey(e => new { e.Name, e.GuildId, e.CreatedAt, e.RoleId });

        builder.HasOne(e => e.CustomCommand)
            .WithMany(e => e.Roles)
            .HasForeignKey(e => new { e.Name, e.GuildId, e.CreatedAt })
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasConversion(name => name.Value, value => CustomCommandName.FromDatabase(value))
            .HasMaxLength(24);

        builder.HasDiscriminator<string>("RoleType")
            .HasValue<CustomCommandAllowRole>("Allow")
            .HasValue<CustomCommandDenyRole>("Deny")
            .IsComplete();
    }
}

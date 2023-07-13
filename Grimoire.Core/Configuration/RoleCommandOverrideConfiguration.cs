// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Core.Configuration;

[ExcludeFromCodeCoverage]
internal class RoleCommandOverrideConfiguration : IEntityTypeConfiguration<RoleCommandOverride>
{
    public void Configure(EntityTypeBuilder<RoleCommandOverride> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityAlwaysColumn();
        builder.HasIndex(e => new { e.RoleId, e.GuildId, e.ChannelId })
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasOne(e => e.Role)
            .WithMany(e => e.RoleCommandOverrides)
            .HasForeignKey(e => e.RoleId)
            .IsRequired();
        builder.HasOne(e => e.Guild)
            .WithMany(e => e.RoleCommandOverrides)
            .HasForeignKey(e => e.GuildId)
            .IsRequired();
        builder.HasOne(e => e.Channel)
            .WithMany(e => e.RoleCommandOverrides)
            .HasForeignKey(e => e.ChannelId)
            .IsRequired(false);
        builder.Property(e => e.CommandPermissions)
            .IsRequired();
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class GuildCommandSetttingsConfiguration : IEntityTypeConfiguration<GuildCommandsSettings>
{
    public void Configure(EntityTypeBuilder<GuildCommandsSettings> builder)
    {
        builder.HasKey(x => x.GuildId);
        builder.HasOne(e => e.Guild).WithOne(e => e.CommandsSettings)
            .HasForeignKey<GuildCommandsSettings>(x => x.GuildId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.Property(x => x.ModuleEnabled)
            .HasDefaultValue(false);
    }
}

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

internal sealed class CommandSettingsConfiguration : IEntityTypeConfiguration<CustomCommandsSettings>
{
    public void Configure(EntityTypeBuilder<CustomCommandsSettings> builder)
    {
        builder.HasKey(x => x.GuildId);
        builder.Property(x => x.ModuleEnabled)
            .HasDefaultValue(false);
    }
}

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
internal sealed class ModerationSettingsConfiguration : IEntityTypeConfiguration<ModerationSettings>
{
    public void Configure(EntityTypeBuilder<ModerationSettings> builder)
    {
        builder.HasKey(e => e.GuildId);
        builder.Property(e => e.PublicBanLog)
            .IsRequired(false);
        builder.Property(e => e.AutoPardonAfter)
            .HasDefaultValue(TimeSpan.FromDays(30 * 365));
        builder.Property(x => x.ModuleEnabled)
            .HasDefaultValue(false);
    }
}

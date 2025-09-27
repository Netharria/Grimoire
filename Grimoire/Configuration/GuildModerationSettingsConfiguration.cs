// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Grimoire.Domain.Obsolete;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class GuildModerationSettingsConfiguration : IEntityTypeConfiguration<GuildModerationSettings>
{
    public void Configure(EntityTypeBuilder<GuildModerationSettings> builder)
    {
        builder.HasKey(e => e.GuildId);
        builder.Property(e => e.PublicBanLog)
            .IsRequired(false);
        builder.Property(e => e.AutoPardonAfter)
            .HasDefaultValue(TimeSpan.FromDays(30 * 365));
        builder.HasOne(e => e.MuteRoleNav)
            .WithOne()
            .HasForeignKey<GuildModerationSettings>(e => e.MuteRole)
            .IsRequired(false);
        builder.Property(x => x.ModuleEnabled)
            .HasDefaultValue(false);
    }
}

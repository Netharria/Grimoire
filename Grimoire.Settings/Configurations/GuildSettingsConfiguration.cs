// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Grimoire.Domain;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

[ExcludeFromCodeCoverage]
internal sealed class GuildSettingsConfiguration : IEntityTypeConfiguration<GuildSettings>
{
    public void Configure(EntityTypeBuilder<GuildSettings> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedNever()
            .IsRequired();


        builder.Property(e => e.Id)
            .HasConversion(e => e.Value, value => new GuildId(value));
        builder.Property(e => e.ModLogChannelId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ChannelId(value));
        builder.Property(e => e.UserCommandChannelId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ChannelId(value));
    }
}

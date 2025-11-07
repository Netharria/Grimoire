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
internal sealed class MuteConfiguration : IEntityTypeConfiguration<Mute>
{
    public void Configure(EntityTypeBuilder<Mute> builder)
    {
        builder.HasKey(e => e.SinId);

        builder.Property(e => e.SinId).ValueGeneratedNever();
        builder.Property(e => e.EndTime).IsRequired();
        builder.HasIndex(x => x.EndTime);


        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));
        builder.Property(e => e.UserId)
            .HasConversion(e => e.Value, value => new UserId(value));

        builder.Property(e => e.SinId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new SinId(value));
    }
}

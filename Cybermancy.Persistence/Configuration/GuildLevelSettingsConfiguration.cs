// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class GuildLevelSettingsConfiguration : IEntityTypeConfiguration<GuildLevelSettings>
    {
        public void Configure(EntityTypeBuilder<GuildLevelSettings> builder)
        {
            builder.HasKey(x => x.GuildId);
            builder.Property(x => x.IsLevelingEnabled).HasDefaultValue(value: false);
            builder.HasOne(e => e.Guild).WithOne(e => e.LevelSettings)
                .HasForeignKey<GuildLevelSettings>(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.Property(e => e.TextTime)
                .HasDefaultValue(3);
            builder.Property(e => e.Base)
                .HasDefaultValue(15);
            builder.Property(e => e.Modifier)
                .HasDefaultValue(50);
            builder.Property(e => e.Amount)
                .HasDefaultValue(5);
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="GuildLevelSettingsConfiguration.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Persistence.Configuration
{
    using System.Diagnostics.CodeAnalysis;
    using Cybermancy.Domain;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
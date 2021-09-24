// -----------------------------------------------------------------------
// <copyright file="GuildModerationSettingsConfiguration.cs" company="Netharia">
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
    internal class GuildModerationSettingsConfiguration : IEntityTypeConfiguration<GuildModerationSettings>
    {
        public void Configure(EntityTypeBuilder<GuildModerationSettings> builder)
        {
            builder.HasKey(e => e.GuildId);
            builder.HasOne(e => e.Guild).WithOne(e => e.ModerationSettings)
                .HasForeignKey<GuildModerationSettings>(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.Property(e => e.DurationType)
                .HasDefaultValue(Duration.Years);
            builder.Property(e => e.Duration)
                .HasDefaultValue(30);
        }
    }
}
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

namespace Cybermancy.Core.Configuration
{
    [ExcludeFromCodeCoverage]
    internal class GuildModerationSettingsConfiguration : IEntityTypeConfiguration<GuildModerationSettings>
    {
        public void Configure(EntityTypeBuilder<GuildModerationSettings> builder)
        {
            builder.HasKey(e => e.GuildId);
            builder.HasOne(e => e.Guild)
                .WithOne(e => e.ModerationSettings)
                .HasForeignKey<GuildModerationSettings>(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.Property(e => e.PublicBanLog)
                .IsRequired(false);
            builder.Property(e => e.DurationType)
                .HasDefaultValue(Duration.Years);
            builder.Property(e => e.Duration)
                .HasDefaultValue(30);
            builder.Property(e => e.MuteRole)
                .IsRequired(false);
            builder.Property(x => x.IsModerationEnabled)
                .HasDefaultValue(value: false);
        }
    }
}

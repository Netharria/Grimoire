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
    public class GuildLogSettingsConfiguration : IEntityTypeConfiguration<GuildLogSettings>
    {
        public void Configure(EntityTypeBuilder<GuildLogSettings> builder)
        {
            builder.HasKey(e => e.GuildId);
            builder.HasOne(e => e.Guild).WithOne(e => e.LogSettings)
                .HasForeignKey<GuildLogSettings>(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.Property(x => x.IsLoggingEnabled).HasDefaultValue(value: false);
        }
    }
}

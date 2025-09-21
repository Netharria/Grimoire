// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class GuildMessageLogSettingsConfiguration : IEntityTypeConfiguration<GuildMessageLogSettings>
{
    public void Configure(EntityTypeBuilder<GuildMessageLogSettings> builder)
    {
        builder.HasKey(e => e.GuildId);
        builder.HasOne(e => e.Guild)
            .WithOne()
            .HasForeignKey<GuildMessageLogSettings>(e => e.GuildId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne(e => e.DeleteChannelLog)
            .WithOne()
            .HasForeignKey<GuildMessageLogSettings>(e => e.DeleteChannelLogId)
            .IsRequired(false);
        builder.HasOne(e => e.BulkDeleteChannelLog)
            .WithOne()
            .HasForeignKey<GuildMessageLogSettings>(e => e.BulkDeleteChannelLogId)
            .IsRequired(false);
        builder.HasOne(e => e.EditChannelLog)
            .WithOne()
            .HasForeignKey<GuildMessageLogSettings>(e => e.EditChannelLogId)
            .IsRequired(false);
        builder.Property(x => x.ModuleEnabled)
            .HasDefaultValue(false);
    }
}

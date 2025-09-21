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
internal sealed class GuildConfiguration : IEntityTypeConfiguration<Guild>
{
    public void Configure(EntityTypeBuilder<Guild> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedNever()
            .IsRequired();
        builder.HasOne(e => e.ModLogChannel)
            .WithMany()
            .HasForeignKey(e => e.ModChannelLog)
            .IsRequired(false);
        builder.HasOne(e => e.UserCommandChannel)
            .WithMany()
            .HasForeignKey(e => e.UserCommandChannelId)
            .IsRequired(false);
    }
}

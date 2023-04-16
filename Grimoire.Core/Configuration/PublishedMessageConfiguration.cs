// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Core.Configuration
{
    [ExcludeFromCodeCoverage]
    public class PublishedMessageConfiguration : IEntityTypeConfiguration<PublishedMessage>
    {
        public void Configure(EntityTypeBuilder<PublishedMessage> builder)
        {
            builder.HasKey(e => new { e.SinId, e.PublishType });
            builder.HasOne(e => e.Sin)
                .WithMany(e => e.PublishMessages)
                .HasForeignKey(e => e.SinId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.Property(e => e.PublishType)
                .IsRequired();
        }
    }
}

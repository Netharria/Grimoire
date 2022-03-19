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
    public class PublishedMessageConfiguration : IEntityTypeConfiguration<PublishedMessage>
    {
        public void Configure(EntityTypeBuilder<PublishedMessage> builder)
        {
            builder.HasKey(e => e.MessageId);
            builder.Property(e => e.MessageId)
                .ValueGeneratedNever()
                .IsRequired();
            builder.HasIndex(e => new { e.SinId, e.PublishType })
                .IsUnique();
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

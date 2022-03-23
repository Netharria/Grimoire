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
    public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> builder)
        {
            builder.HasKey(e => new { e.MessageId, e.AttachmentUrl });
            builder.HasOne(e => e.Message)
                .WithMany(x => x.Attachments)
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.Property(e => e.AttachmentUrl)
                .HasMaxLength(400)
                .IsRequired();
        }
    }
}
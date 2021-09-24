// -----------------------------------------------------------------------
// <copyright file="AttachmentConfiguration.cs" company="Netharia">
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
    public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> builder)
        {
            builder.HasKey(e => e.MessageId);
            builder.HasOne(e => e.Message).WithMany(x => x.Attachments)
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.Property(e => e.AttachmentUrl)
                .IsRequired();
        }
    }
}
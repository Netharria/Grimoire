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
internal sealed class MessageHistoryConfiguration : IEntityTypeConfiguration<MessageHistoryEntry>
{
    public void Configure(EntityTypeBuilder<MessageHistoryEntry> builder)
    {
        builder.HasKey(x => new { x.MessageId, x.Timestamp });

        builder.HasDiscriminator<string>("Discriminator")
            .HasValue<MessageCreatedEntry>("Created")
            .HasValue<MessageEditedEntry>("Edited")
            .HasValue<MessageDeletedEntry>("Deleted")
            .HasValue<MessageDeletedByModeratorEntry>("DeletedByModerator");

        builder.HasOne(x => x.Message)
            .WithMany(x => x.MessageHistory)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.Property(e => e.Timestamp)
            .HasColumnName("TimeStamp");
    }
}

[ExcludeFromCodeCoverage]
internal sealed class MessageDeletedByModeratorEntryConfiguration : IEntityTypeConfiguration<MessageDeletedByModeratorEntry>
{
    public void Configure(EntityTypeBuilder<MessageDeletedByModeratorEntry> builder)
        => builder.Property(e => e.ModeratorId)
            .HasColumnName("ModeratorId");
}

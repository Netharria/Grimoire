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

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));

        builder.Property(e => e.MessageId)
            .HasConversion(e => e.Value, value => new MessageId(value));

        builder.Property<MessageContent>("Content")
            .HasColumnName("Content")
            .HasMaxLength(4000)
            .HasConversion(c => c.Content, v => MessageContent.FromDatabase(v));

        builder.Property<ModeratorId?>("ModeratorId")
            .HasColumnName("ModeratorId")
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ModeratorId(value));
    }
}

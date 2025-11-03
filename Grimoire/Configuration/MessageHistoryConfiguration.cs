// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class MessageHistoryConfiguration : IEntityTypeConfiguration<MessageHistory>
{
    public void Configure(EntityTypeBuilder<MessageHistory> builder)
    {
        builder.HasKey(x => new { x.MessageId, x.TimeStamp });
        builder.HasOne(x => x.Message)
            .WithMany(x => x.MessageHistory)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.Property(x => x.Action)
            .IsRequired();
        builder.Property(x => x.MessageContent)
            .HasMaxLength(4000);
        builder.Property(e => e.TimeStamp)
            .HasDefaultValueSql("now()");


        builder.Property(e => e.DeletedByModeratorId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ModeratorId(value));

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));

        builder.Property(e => e.MessageId)
            .HasConversion(e => e.Value, value => new MessageId(value));


        builder.Property(e => e.MessageContent)
            .HasConversion(e => e.GetValueOrDefault().Content, value => new MessageContent(value));
    }
}

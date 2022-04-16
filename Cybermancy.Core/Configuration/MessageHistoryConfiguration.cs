// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Core.Configuration
{
    public class MessageHistoryConfiguration : IEntityTypeConfiguration<MessageHistory>
    {
        public void Configure(EntityTypeBuilder<MessageHistory> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(e => e.Id)
                .HasColumnType("bigint")
                .UseIdentityAlwaysColumn();
            builder.HasOne(x => x.Message)
                .WithMany(x => x.MessageHistory)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.Property(x => x.Action)
                .IsRequired();
            builder.Property(x => x.MessageContent)
                .HasMaxLength(4000);
            builder.HasOne(e => e.DeletedByModerator)
                .WithMany(e => e.MessagesDeletedAsModerator)
                .HasForeignKey(e => new { e.GuildId, e.DeletedByModeratorId})
                .HasPrincipalKey(e => new { e.GuildId, e.UserId})
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            builder.Property(e => e.TimeStamp)
                .HasDefaultValueSql("now()");
        }
    }
}

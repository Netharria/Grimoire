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
internal sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> builder)
    {
        builder.HasKey(e => new { e.MessageId, e.UserId, e.Emoji });

        builder.Property(e => e.MessageId)
            .HasConversion(id => id.Value, value => new MessageId(value));
        builder.Property(e => e.UserId)
            .HasConversion(id => id.Value, value => new UserId(value));
        builder.Property(e => e.GuildId)
            .HasConversion(id => id.Value, value => new GuildId(value));
        builder.Property(e => e.Emoji)
            .HasConversion(emoji => emoji.ToString(), value => ReactionEmoji.ParseFromDatabase(value))
            .HasMaxLength(ReactionEmoji.MaxLength)
            .IsRequired();

        builder.Ignore(e => e.IsRemoved);

        builder.HasOne(e => e.Message)
            .WithMany(message => message.Reactions)
            .HasForeignKey(e => e.MessageId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Core.Configuration;

internal sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> builder)
    {
        builder.HasKey(x => new { x.MessageId, x.EmojiId });
        builder.HasOne(x => x.Message)
            .WithMany(x => x.Reactions)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Member)
            .WithMany(x => x.Reactions)
            .HasForeignKey(x => new { x.UserId, x.GuildId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Name)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.ImageUrl)
            .HasMaxLength(300)
            .IsRequired();
    }
}

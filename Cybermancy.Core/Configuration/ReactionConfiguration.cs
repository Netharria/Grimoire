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
    public class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
    {
        public void Configure(EntityTypeBuilder<Reaction> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .ValueGeneratedOnAdd();
            builder.HasOne(x => x.Message)
                .WithMany(x => x.Reactions)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.GuildUser)
                .WithMany(x => x.Reactions)
                .HasForeignKey(x => x.GuildUserId)
                .HasPrincipalKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Property(x => x.Name)
                .HasMaxLength(32)
                .IsRequired();
            builder.Property(x => x.ImageUrl)
                .HasMaxLength(300)
                .IsRequired();
        }
    }
}

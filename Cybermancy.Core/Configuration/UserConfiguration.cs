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
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever().IsRequired();
            builder.Property(e => e.UserName)
                .HasMaxLength(32)
                .IsRequired();
            builder.Property(e => e.AvatarUrl).HasMaxLength(300).IsRequired();
            builder.HasMany(e => e.Messages).WithOne(e => e.User);
            builder.HasMany(e => e.Trackers).WithOne(e => e.User).HasForeignKey(e => e.UserId);
            builder.HasMany(e => e.TrackedUsers).WithOne(e => e.Moderator).HasForeignKey(e => e.ModeratorId);
            builder.HasMany(e => e.UserSins).WithOne(e => e.User).HasForeignKey(e => e.UserId);
            builder.HasMany(e => e.ModeratedSins).WithOne(e => e.Moderator).HasForeignKey(e => e.ModeratorId);
            builder.HasMany(e => e.ChannelsLocked).WithOne(e => e.Moderator);
            builder.HasMany(e => e.SinsPardoned).WithOne(e => e.Moderator);
        }
    }
}

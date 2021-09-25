// -----------------------------------------------------------------------
// <copyright file="SinConfiguration.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class SinConfiguration : IEntityTypeConfiguration<Sin>
    {
        public void Configure(EntityTypeBuilder<Sin> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedOnAdd();
            builder.HasOne(e => e.User).WithMany(e => e.UserSins)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.Moderator).WithMany(e => e.ModeratedSins)
                .HasForeignKey(e => e.ModeratorId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(e => e.Guild).WithMany(e => e.Sins)
                .HasForeignKey(e => e.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.Mute).WithOne(e => e.Sin).IsRequired(false);
            builder.HasOne(e => e.Pardon).WithOne(e => e.Sin).IsRequired(false);
            builder.HasMany(e => e.PublishMessages).WithOne(e => e.Sin);
        }
    }
}
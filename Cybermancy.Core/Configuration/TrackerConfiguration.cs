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
    public class TrackerConfiguration : IEntityTypeConfiguration<Tracker>
    {
        public void Configure(EntityTypeBuilder<Tracker> builder)
        {
            builder.HasKey(e => new { e.UserId, e.GuildId });
            builder.HasOne(e => e.Member)
                .WithMany(e => e.Trackers)
                .HasForeignKey(e => new { e.UserId, e.GuildId })
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.LogChannel)
                .WithMany(e => e.Trackers)
                .HasForeignKey(e => e.LogChannelId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.Moderator)
                .WithMany(e => e.TrackedUsers)
                .HasForeignKey(e => new { e.ModeratorId, e.GuildId })
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            builder.Property(e => e.EndTime)
                .IsRequired();
        }
    }
}

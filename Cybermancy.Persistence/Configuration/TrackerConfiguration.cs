﻿using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class TrackerConfiguration : IEntityTypeConfiguration<Tracker>
    {
        public void Configure(EntityTypeBuilder<Tracker> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new {e.UserId, e.GuildId})
                .IsUnique();
            builder.HasOne(e => e.User).WithMany(e => e.Trackers).HasForeignKey(e => e.UserId);
            builder.HasOne(e => e.Guild).WithMany(e => e.Trackers).HasForeignKey(e => e.GuildId);
            builder.HasOne(e => e.LogChannel).WithMany(e => e.Trackers);
            builder.HasOne(e => e.Moderator).WithMany(e => e.TrackedUsers).HasForeignKey(e => e.ModeratorId);
        }
    }
}
// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Grimoire.Domain.Obsolete;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class TrackerConfiguration : IEntityTypeConfiguration<Tracker>
{
    public void Configure(EntityTypeBuilder<Tracker> builder)
    {
        builder.HasKey(e => new { e.UserId, e.GuildId });
        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => new { e.UserId, e.GuildId })
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne(e => e.LogChannel)
            .WithMany()
            .HasForeignKey(e => e.LogChannelId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne(e => e.Moderator)
            .WithMany()
            .HasForeignKey(e => new { e.ModeratorId, e.GuildId })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
        builder.Property(e => e.EndTime)
            .IsRequired();
        builder.HasIndex(x => x.EndTime);
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Core.Configuration;

internal sealed class XpHistoryConfiguration : IEntityTypeConfiguration<XpHistory>
{
    public void Configure(EntityTypeBuilder<XpHistory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .UseIdentityAlwaysColumn();
        builder.HasIndex(x => new { x.UserId, x.GuildId, x.TimeOut })
            .IsDescending(false, false, true)
            .IncludeProperties(x => x.Xp);
        builder.HasOne(x => x.Member)
            .WithMany(x => x.XpHistory)
            .HasForeignKey(x => new { x.UserId, x.GuildId })
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Guild)
            .WithMany(x => x.XpHistory)
            .HasForeignKey(x => x.GuildId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Xp)
            .IsRequired();
        builder.Property(x => x.TimeOut)
            .IsRequired();
        builder.Property(x => x.Type)
            .IsRequired();
        builder.HasOne(x => x.Awarder)
            .WithMany(x => x.AwardRecipients)
            .HasForeignKey(x => new { x.AwarderId, x.GuildId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class NicknameHistoryConfiguration : IEntityTypeConfiguration<NicknameHistory>
{
    public void Configure(EntityTypeBuilder<NicknameHistory> builder)
    {
        builder.HasKey(x => new { x.UserId, x.GuildId, x.Timestamp });
        builder.HasOne(x => x.Member)
            .WithMany(x => x.NicknamesHistory)
            .HasForeignKey(x => new { x.UserId, x.GuildId })
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Nickname)
            .HasMaxLength(32)
            .IsRequired(false);
        builder.Property(x => x.Timestamp)
            .HasDefaultValueSql("now()");
    }
}

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
        builder.Property(x => x.Nickname)
            .HasMaxLength(32)
            .IsRequired(false);
        builder.Property(x => x.Timestamp)
            .HasDefaultValueSql("now()");

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));

        builder.Property(e => e.UserId)
            .HasConversion(e => e.Value, value => new UserId(value));


        builder.Property(e => e.Nickname)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new Nickname(value));
    }
}

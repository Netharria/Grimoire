// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class ServerDisplayNameHistoryConfiguration : IEntityTypeConfiguration<ServerDisplayNameHistory>
{
    public void Configure(EntityTypeBuilder<ServerDisplayNameHistory> builder)
    {
        builder.HasKey(x => new { x.UserId, x.GuildId, x.Timestamp });
        builder.Property(x => x.DisplayName)
            .HasMaxLength(32)
            .IsRequired(false);
        builder.Property(x => x.Timestamp)
            .HasDefaultValueSql("now()");

        builder.Property(e => e.DisplayName)
            .HasConversion(e => e.GetValueOrDefault().Value, value => Nickname.FromDatabase(value));
    }
}

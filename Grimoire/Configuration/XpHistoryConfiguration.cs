// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class XpHistoryConfiguration : IEntityTypeConfiguration<XpHistory>
{
    public void Configure(EntityTypeBuilder<XpHistory> builder)
    {
        builder.HasKey(x => new { x.UserId, x.GuildId, x.TimeOut });
        builder.Property(x => x.Xp)
            .IsRequired();
        builder.Property(x => x.TimeOut)
            .IsRequired();
        builder.Property(x => x.Type)
            .IsRequired();
    }
}

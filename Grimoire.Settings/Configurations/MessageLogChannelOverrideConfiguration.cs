// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

internal sealed class MessageLogChannelOverrideConfiguration : IEntityTypeConfiguration<MessageLogChannelOverride>
{
    public void Configure(EntityTypeBuilder<MessageLogChannelOverride> builder)
    {
        builder.HasKey(x => new { x.ChannelId, x.GuildId, x.SetAt });
        builder.Property(x => x.ChannelId)
            .ValueGeneratedNever();
        builder.Property(x => x.ChannelOption)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(e => e.SetBy)
            .HasConversion(e => e.Value, value => new ModeratorId(value));

        builder.HasIndex(x => new { x.ChannelId, x.GuildId, x.SetAt })
            .IsDescending(false, false, true);

        builder.HasIndex(x => x.GuildId);
        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));
        builder.Property(e => e.ChannelId)
            .HasConversion(e => e.Value, value => new ChannelId(value));
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

internal sealed class IgnoredChannelConfiguration : IEntityTypeConfiguration<IgnoredChannel>
{
    public void Configure(EntityTypeBuilder<IgnoredChannel> builder)
    {
        builder.HasKey(e => e.ChannelId);
        builder.Property(e => e.ChannelId)
            .ValueGeneratedNever()
            .IsRequired();
        builder.Ignore(channel => channel.Id);

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));
        builder.Property(e => e.ChannelId)
            .HasConversion(e => e.Value, value => new ChannelId(value));
    }
}

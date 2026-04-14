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

internal sealed class XpIgnoredItemsConfiguration : IEntityTypeConfiguration<XpIgnoredItem>
{
    public void Configure(EntityTypeBuilder<XpIgnoredItem> builder)
    {
        builder.HasKey(e => new { e.GuildId, e.Id, e.SetAt });
        builder.Property(e => e.Id)
            .ValueGeneratedNever()
            .IsRequired();

        builder.HasIndex(e => new { e.GuildId, e.Id, e.SetAt })
            .IsDescending(false, false, true);

        builder.Property(e => e.GuildId)
            .ValueGeneratedNever()
            .HasConversion(e => e.Value, value => new GuildId(value))
            .IsRequired();

        builder.Property(e => e.SetBy)
            .HasConversion(e => e.Value, value => new ModeratorId(value));

        builder.Ignore("ChannelId");
        builder.Ignore("RoleId");
        builder.Ignore("UserId");

        builder.HasDiscriminator<string>("Type")
            .HasValue<IgnoredChannel>(nameof(IgnoredType.Channel))
            .HasValue<IgnoredRole>(nameof(IgnoredType.Role))
            .HasValue<IgnoredMember>(nameof(IgnoredType.Member));
    }
}

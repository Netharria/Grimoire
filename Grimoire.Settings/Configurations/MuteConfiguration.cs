// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

[ExcludeFromCodeCoverage]
internal sealed class MuteConfiguration : IEntityTypeConfiguration<Mute>, IEntityTypeConfiguration<MuteAdded>
{
    public void Configure(EntityTypeBuilder<Mute> builder)
    {
        builder.HasKey(e => new { e.UserId, e.GuildId, e.SetAt });

        builder.HasDiscriminator<string>("EventType")
            .HasValue<MuteAdded>("Added")
            .HasValue<MuteRemoved>("Removed")
            .IsComplete();

        builder.Property(e => e.GuildId);
        builder.Property(e => e.UserId);
        builder.Property(e => e.ModeratorId);
    }

    public void Configure(EntityTypeBuilder<MuteAdded> builder)
    {
        // Only open MuteAdded rows, so the expiry poll's cost follows what is in force, not the history (review S-13).
        builder.HasIndex(x => x.EndTime).HasFilter("\"EventType\" = 'Added' AND \"ClosedAt\" IS NULL");

        builder.Property(e => e.SinId);

        builder.Property(e => e.MuteRoleId);
    }
}

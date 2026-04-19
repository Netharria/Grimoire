// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Values;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

[ExcludeFromCodeCoverage]
internal sealed class RewardConfiguration : IEntityTypeConfiguration<Reward>
{
    public void Configure(EntityTypeBuilder<Reward> builder)
    {
        builder.HasKey(e => new { e.GuildId, e.RoleId, e.SetAt });
        builder.Property(e => e.RewardMessage)
            .HasConversion(
                r => r == null ? null : r.Value.Value,
                v => v == null ? null : RewardMessage.FromDatabase(v))
            .HasMaxLength(4096)
            .IsRequired(false);
        builder.Property(e => e.RewardLevel).IsRequired();
        builder.Property(e => e.SetBy)
            .HasConversion(e => e.Value, value => new ModeratorId(value));
        builder.HasIndex(x => new { x.GuildId, x.RoleId, x.SetAt })
            .IsDescending(false, false, true);

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));
        builder.Property(e => e.RoleId)
            .HasConversion(e => e.Value, value => new RoleId(value));
    }
}

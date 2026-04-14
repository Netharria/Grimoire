// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Settings.Configurations;

internal sealed class GuildSettingConfiguration : IEntityTypeConfiguration<GuildSetting>
{
    public void Configure(EntityTypeBuilder<GuildSetting> builder)
    {
        builder.HasKey(guildSetting => new
        {
            Key = guildSetting.Type, guildSetting.GuildId, UpdatedAt = guildSetting.SetAt
        });

        builder.HasDiscriminator<string>("State")
            .HasValue<GuildSettingDefault>("Default")
            .HasValue<GuildSettingDisabled>("Disabled")
            .HasValue<GuildSettingCustomValue>("CustomValue");

        builder.HasIndex(x => new { x.GuildId, Key = x.Type, x.SetAt })
            .IsDescending(false, false, true);

        builder.Property(guildSetting => guildSetting.GuildId)
            .HasConversion(guildSetting => guildSetting.Value,
                value => new GuildId(value));

        builder.Property(e => e.SetBy)
            .HasConversion(e => e.Value, value => new ModeratorId(value));

        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(64);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_GuildSettings_State_Value",
            @"(""State"" = 'CustomValue' AND ""Value"" IS NOT NULL)
                OR (""State"" IN ('Default', 'Disabled') AND ""Value"" IS NULL)"));
    }
}

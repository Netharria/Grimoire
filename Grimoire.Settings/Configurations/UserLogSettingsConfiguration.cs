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
internal sealed class UserLogSettingsConfiguration : IEntityTypeConfiguration<UserLogSettings>
{
    public void Configure(EntityTypeBuilder<UserLogSettings> builder)
    {
        builder.HasKey(e => e.GuildId);
        builder.Property(x => x.ModuleEnabled)
            .HasDefaultValue(false);

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));
        builder.Property(e => e.JoinChannelLogId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ChannelId(value));
        builder.Property(e => e.LeaveChannelLogId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ChannelId(value));
        builder.Property(e => e.UsernameChannelLogId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ChannelId(value));
        builder.Property(e => e.NicknameChannelLogId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ChannelId(value));
        builder.Property(e => e.AvatarChannelLogId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ChannelId(value));
    }
}

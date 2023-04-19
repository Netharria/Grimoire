// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Core.Configuration
{
    [ExcludeFromCodeCoverage]
    public class GuildUserLogSettingsConfiguration : IEntityTypeConfiguration<GuildUserLogSettings>
    {
        public void Configure(EntityTypeBuilder<GuildUserLogSettings> builder)
        {
            builder.HasKey(e => e.GuildId);
            builder.HasOne(e => e.Guild)
                .WithOne(e => e.UserLogSettings)
                .HasForeignKey<GuildUserLogSettings>(e => e.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.JoinChannelLog)
                .WithOne()
                .HasForeignKey<GuildUserLogSettings>(e => e.JoinChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.LeaveChannelLog)
                .WithOne()
                .HasForeignKey<GuildUserLogSettings>(e => e.LeaveChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.UsernameChannelLog)
                .WithOne()
                .HasForeignKey<GuildUserLogSettings>(e => e.UsernameChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.NicknameChannelLog)
                .WithOne()
                .HasForeignKey<GuildUserLogSettings>(e => e.NicknameChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.AvatarChannelLog)
                .WithOne()
                .HasForeignKey<GuildUserLogSettings>(e => e.AvatarChannelLogId)
                .IsRequired(false);
            builder.Property(x => x.ModuleEnabled)
                .HasDefaultValue(value: false);
        }
    }
}

// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Core.Configuration
{
    [ExcludeFromCodeCoverage]
    public class GuildLogSettingsConfiguration : IEntityTypeConfiguration<GuildLogSettings>
    {
        public void Configure(EntityTypeBuilder<GuildLogSettings> builder)
        {
            builder.HasKey(e => e.GuildId);
            builder.HasOne(e => e.Guild)
                .WithOne(e => e.LogSettings)
                .HasForeignKey<GuildLogSettings>(e => e.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.JoinChannelLog)
                .WithOne()
                .HasForeignKey<GuildLogSettings>(e => e.JoinChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.LeaveChannelLog)
                .WithOne()
                .HasForeignKey<GuildLogSettings>(e => e.LeaveChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.DeleteChannelLog)
                .WithOne()
                .HasForeignKey<GuildLogSettings>(e => e.DeleteChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.BulkDeleteChannelLog)
                .WithOne()
                .HasForeignKey<GuildLogSettings>(e => e.BulkDeleteChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.EditChannelLog)
                .WithOne()
                .HasForeignKey<GuildLogSettings>(e => e.EditChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.UsernameChannelLog)
                .WithOne()
                .HasForeignKey<GuildLogSettings>(e => e.UsernameChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.NicknameChannelLog)
                .WithOne()
                .HasForeignKey<GuildLogSettings>(e => e.NicknameChannelLogId)
                .IsRequired(false);
            builder.HasOne(e => e.AvatarChannelLog)
                .WithOne()
                .HasForeignKey<GuildLogSettings>(e => e.AvatarChannelLogId)
                .IsRequired(false);
            builder.Property(x => x.IsLoggingEnabled)
                .HasDefaultValue(value: false);
        }
    }
}

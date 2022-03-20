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
    public class GuildUserConfigurations : IEntityTypeConfiguration<GuildUser>
    {
        public void Configure(EntityTypeBuilder<GuildUser> builder)
        {
            builder.HasKey(e => new { e.UserId, e.GuildId });
            builder.HasOne(e => e.Guild)
                .WithMany(e => e.GuildUsers)
                .HasForeignKey(e => e.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.User)
                .WithMany(e => e.GuildMemberProfiles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.Property(e => e.DisplayName)
                .HasField("_displayName")
                .HasMaxLength(32)
                .IsRequired(false);
            builder.Property(e => e.GuildAvatarUrl)
                .HasField("_guildAvatarUrl")
                .HasMaxLength(300)
                .IsRequired(false);
            builder.Property(e => e.Xp)
                .IsRequired()
                .HasDefaultValue(value: 0);
            builder.Property(e => e.TimeOut)
                .IsRequired();
            builder.Property(e => e.IsXpIgnored)
                .HasDefaultValue(value: false);
        }
    }
}

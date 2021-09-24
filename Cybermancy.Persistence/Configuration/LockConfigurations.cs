// -----------------------------------------------------------------------
// <copyright file="LockConfigurations.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Persistence.Configuration
{
    using System.Diagnostics.CodeAnalysis;
    using Cybermancy.Domain;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    [ExcludeFromCodeCoverage]
    public class LockConfigurations : IEntityTypeConfiguration<Lock>
    {
        public void Configure(EntityTypeBuilder<Lock> builder)
        {
            builder.HasKey(e => e.ChannelId);
            builder.HasOne(e => e.Channel).WithOne(e => e.Lock)
                .HasForeignKey<Lock>(x => x.ChannelId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.HasOne(e => e.Moderator).WithMany(e => e.ChannelsLocked)
                .HasForeignKey(e => e.ModeratorId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            builder.HasOne(e => e.Guild).WithMany(e => e.LockedChannels)
                .HasForeignKey(e => e.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        }
    }
}
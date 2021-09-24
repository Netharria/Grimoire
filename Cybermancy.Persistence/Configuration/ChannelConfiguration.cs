﻿// -----------------------------------------------------------------------
// <copyright file="ChannelConfiguration.cs" company="Netharia">
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
    public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
    {
        public void Configure(EntityTypeBuilder<Channel> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever()
                .IsRequired();

            builder.Property(e => e.Name).IsRequired();

            builder.HasOne(e => e.Guild).WithMany(e => e.Channels)
                .HasForeignKey(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.Property(e => e.IsXpIgnored)
                .HasDefaultValue(value: false);

            builder.HasMany(e => e.Messages).WithOne(e => e.Channel);
            builder.HasMany(e => e.Trackers).WithOne(e => e.LogChannel);

            builder.HasOne(e => e.Lock).WithOne(e => e.Channel).IsRequired(false);
        }
    }
}
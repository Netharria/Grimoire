// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Core.Configuration
{
    [ExcludeFromCodeCoverage]
    public class OldLogMessageConfiguration : IEntityTypeConfiguration<OldLogMessage>
    {
        public void Configure(EntityTypeBuilder<OldLogMessage> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                .ValueGeneratedNever()
                .IsRequired();
            builder.HasOne(e => e.Channel)
                .WithMany(e => e.OldMessages)
                .HasForeignKey(x => x.ChannelId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(e => e.Guild)
                .WithMany(e => e.OldLogMessages)
                .HasForeignKey(x => x.GuildId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");
        }
    }
}

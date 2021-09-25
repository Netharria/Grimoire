// -----------------------------------------------------------------------
// <copyright file="OldLogMessageConfiguration.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class OldLogMessageConfiguration : IEntityTypeConfiguration<OldLogMessage>
    {
        public void Configure(EntityTypeBuilder<OldLogMessage> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever().IsRequired();
            builder.Property(e => e.ChannelId).IsRequired();
            builder.HasOne(e => e.Guild).WithMany(e => e.OldLogMessages)
                .HasForeignKey(x => x.GuildId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
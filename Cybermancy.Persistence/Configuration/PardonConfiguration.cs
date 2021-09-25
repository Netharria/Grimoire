// -----------------------------------------------------------------------
// <copyright file="PardonConfiguration.cs" company="Netharia">
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
    public class PardonConfiguration : IEntityTypeConfiguration<Pardon>
    {
        public void Configure(EntityTypeBuilder<Pardon> builder)
        {
            builder.HasKey(e => e.SinId);
            builder.HasOne(e => e.Sin).WithOne(e => e.Pardon)
                .HasForeignKey<Pardon>(e => e.SinId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.Moderator).WithMany(e => e.SinsPardoned)
                .HasForeignKey(e => e.ModeratorId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="GuildLogSettingsConfiguration.cs" company="Netharia">
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
    public class GuildLogSettingsConfiguration : IEntityTypeConfiguration<GuildLogSettings>
    {
        public void Configure(EntityTypeBuilder<GuildLogSettings> builder)
        {
            builder.HasKey(e => e.GuildId);
            builder.HasOne(e => e.Guild).WithOne(e => e.LogSettings)
                .HasForeignKey<GuildLogSettings>(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        }
    }
}
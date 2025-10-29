// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Grimoire.Domain.Obsolete;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration.Obsolete;

[ExcludeFromCodeCoverage]
[Obsolete("Table To be Dropped Soon.")]
internal sealed class TrackerConfiguration : IEntityTypeConfiguration<Tracker>
{
    public void Configure(EntityTypeBuilder<Tracker> builder)
    {
        builder.HasKey(e => new { e.UserId, e.GuildId });
        builder.Property(e => e.EndTime)
            .IsRequired();
        builder.HasIndex(x => x.EndTime);
    }
}

// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Lock = Grimoire.Domain.Obsolete.Lock;

namespace Grimoire.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class LockConfigurations : IEntityTypeConfiguration<Lock>
{
    public void Configure(EntityTypeBuilder<Lock> builder)
    {
        builder.HasKey(e => e.ChannelId);
        builder.Property(e => e.PreviouslyAllowed)
            .IsRequired();
        builder.Property(e => e.PreviouslyDenied)
            .IsRequired();
        builder.Property(e => e.Reason)
            .HasMaxLength(1000);
        builder.Property(e => e.EndTime)
            .IsRequired();
        builder.HasIndex(x => x.EndTime);
    }
}

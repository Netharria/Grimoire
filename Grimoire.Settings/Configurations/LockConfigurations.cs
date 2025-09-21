// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Lock = Grimoire.Settings.Domain.Lock;

namespace Grimoire.Settings.Configurations;

[ExcludeFromCodeCoverage]
internal sealed class LockConfigurations : IEntityTypeConfiguration<Lock>
{
    public void Configure(EntityTypeBuilder<Lock> builder)
    {
        builder.HasKey(e => e.ChannelId);
        builder.HasIndex(x => x.EndTime);
    }
}

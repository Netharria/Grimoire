// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Obsolete;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration.Obsolete;

[Obsolete("Table To be Dropped Soon.")]
internal sealed class MessageLogChannelOverrideConfiguration : IEntityTypeConfiguration<MessageLogChannelOverride>
{
    public void Configure(EntityTypeBuilder<MessageLogChannelOverride> builder)
    {
        builder.HasKey(x => x.ChannelId);
        builder.Property(x => x.ChannelId)
            .ValueGeneratedNever();
        builder.Property(x => x.ChannelOption)
            .IsRequired();
    }
}
